using System.Net;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Curation;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Text.Matchers;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Matching;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Processors;

/// <summary>
/// #1416. A Spotify / Apple submit that matches no stored episode, but would match one if that episode's
/// conflicting Spotify / Apple links were set aside, is a <b>service-link conflict</b>. What happens is the
/// caller's <see cref="SubmitOptions.ServiceLinkConflict"/>:
/// <list type="bullet">
/// <item><see cref="ServiceLinkConflictChoice.CreateNew"/> (default): file a new episode, as before #1416.</item>
/// <item><see cref="ServiceLinkConflictChoice.Prompt"/>: write nothing and return a hold for the curator.</item>
/// <item><see cref="ServiceLinkConflictChoice.Cancel"/>: write nothing, even if the conflict has gone since the prompt.</item>
/// <item><see cref="ServiceLinkConflictChoice.Overwrite"/>: split the stored links off into a new episode
/// (<see cref="EpisodeServiceLinks.SplitOff"/>) and enrich the stored episode with the submitted ones. The
/// answer must name the held episode (<see cref="SubmitOptions.ServiceLinkConflictEpisodeId"/>); otherwise the
/// curator is asked again, so a stale answer never overwrites a different episode.</item>
/// </list>
/// </summary>
public class ServiceLinkConflictGate(
    IEpisodeHelper episodeHelper,
    ILogger<ServiceLinkConflictGate> logger) : IServiceLinkConflictGate
{
    public ServiceLinkConflictOutcome Evaluate(CategorisedItem categorisedItem, SubmitOptions submitOptions)
    {
        if (submitOptions.ServiceLinkConflict == ServiceLinkConflictChoice.CreateNew ||
            categorisedItem.MatchingPodcast == null)
        {
            return ServiceLinkConflictOutcome.Proceed;
        }

        if (submitOptions.ServiceLinkConflict == ServiceLinkConflictChoice.Cancel)
        {
            // The curator cancelled this submit: nothing is written, whatever the stored episodes hold by now.
            logger.LogInformation("Service-link conflict answered cancel for podcast '{PodcastId}': nothing written.",
                categorisedItem.MatchingPodcast.Id);
            return ServiceLinkConflictOutcome.Cancelled;
        }

        if (categorisedItem.MatchingEpisode != null)
        {
            return ServiceLinkConflictOutcome.Proceed;
        }

        var episodes = categorisedItem.MatchingPodcastEpisodes?.ToArray() ?? [];
        // #1482: after an overwrite whose stored-episode save failed, the split-off (saved first) holds links and
        // details the stored episode still carries, so it can look like the submitted episode (by title when it lacks
        // a link this attempt resolved) or conflict with it. It is never the episode a retry matches or overwrites:
        // drop any episode whose id is the deterministic split-off id of another listed episode (one still carrying
        // the links it was split from), so the retry overwrites the stored episode again and reuses that split-off.
        // Once the stored episode's overwrite is saved its links differ, so its split-off is an ordinary episode.
        var partialSplitOffs = PartialSplitOffs(episodes);
        var live = episodes.Where(episode => !partialSplitOffs.Contains(episode.Id)).ToArray();
        // #1484: a split-off held for approval (AwaitingApproval or Rejected) matches only by one of its own service ids.
        if (live.Any(episode => SplitOffMatching.MayMatch(episode, categorisedItem) &&
                                episodeHelper.IsMatchingEpisode(episode, categorisedItem)))
        {
            return ServiceLinkConflictOutcome.Proceed;
        }

        var conflicts = live
            // #1484: a split-off held for approval is never a conflict candidate, so an overwrite never lands the
            // submitted links on a hidden episode. The #1482 retry below still looks its split-off up in the
            // unfiltered episodes, so a split-off awaiting approval is reused, not rebuilt.
            .Where(episode => !episode.IsHeldForApproval())
            .Select(episode => (Episode: episode, Keys: episodeHelper.ConflictingServiceKeys(episode, categorisedItem)))
            .Where(conflict => conflict.Keys.Count > 0)
            .ToArray();
        if (conflicts.Length == 0)
        {
            return ServiceLinkConflictOutcome.Proceed;
        }

        var (episode, keys) = conflicts.Length == 1 ? conflicts[0] : Closest(conflicts, categorisedItem);
        switch (submitOptions.ServiceLinkConflict)
        {
            case ServiceLinkConflictChoice.Overwrite when submitOptions.ServiceLinkConflictEpisodeId == episode.Id:
                // #1482: a split-off already saved by an earlier, partly failed overwrite is reused (keeping any
                // curator edits) instead of being rebuilt and saved over.
                var splitOffId = EpisodeServiceLinks.SplitOffId(episode);
                var existing = episodes.FirstOrDefault(candidate => candidate.Id == splitOffId);
                var split = existing is null
                    ? EpisodeServiceLinks.SplitOff(episode, keys)
                    : EpisodeServiceLinks.SplitOffOnto(episode, existing, keys);
                logger.LogWarning(
                    "Service-link conflict with episode '{EpisodeId}': curator chose overwrite. Split {Services} off into {SplitOffKind} episode '{SplitOffEpisodeId}'.",
                    episode.Id, string.Join(", ", split.Moved), split.Reused ? "the existing split-off" : "new",
                    split.Episode.Id);
                return ServiceLinkConflictOutcome.Overwritten(episode, split.Episode, split.SplitOffChanged);
            case ServiceLinkConflictChoice.Overwrite:
                logger.LogWarning(
                    "Service-link conflict is with episode '{EpisodeId}', but the overwrite answer named '{AnsweredEpisodeId}'; asking again.",
                    episode.Id, submitOptions.ServiceLinkConflictEpisodeId);
                return ServiceLinkConflictOutcome.Held(ToHold(episode, keys, categorisedItem));
            default:
                logger.LogInformation(
                    "Service-link conflict with episode '{EpisodeId}' on {Services}: holding for the curator.",
                    episode.Id, string.Join(", ", keys));
                return ServiceLinkConflictOutcome.Held(ToHold(episode, keys, categorisedItem));
        }
    }

    /// <summary>
    /// Ids of listed episodes that are the split-off (<see cref="EpisodeServiceLinks.SplitOffId"/>) of another listed
    /// episode, i.e. of an overwrite whose stored-episode save has not happened yet (#1482). Split-off ids are v5;
    /// ordinary episodes have v4 ids, so most podcasts skip the hashing entirely.
    /// </summary>
    private HashSet<Guid> PartialSplitOffs(Episode[] episodes)
    {
        var splitOffIds = episodes.Where(episode => episode.Id.Version == 5).Select(episode => episode.Id).ToHashSet();
        var partial = new HashSet<Guid>();
        if (splitOffIds.Count == 0)
        {
            return partial;
        }

        foreach (var source in episodes)
        {
            var splitOffId = EpisodeServiceLinks.SplitOffId(source);
            if (splitOffId != source.Id && splitOffIds.Contains(splitOffId) && partial.Add(splitOffId))
            {
                logger.LogInformation(
                    "Service-link conflict: episode '{SplitOffEpisodeId}' is the split-off of a partly saved overwrite of episode '{EpisodeId}' (still carrying the same links), so it is not matched or offered for overwrite.",
                    splitOffId, source.Id);
            }
        }

        return partial;
    }

    /// <summary>
    /// Several conflicting episodes: the closest title wins; on a title tie (#1481: identical titles), the nearest
    /// release, then the nearest duration, then the episode id, so the pick never depends on list order.
    /// </summary>
    internal static (Episode Episode, IReadOnlyList<string> Keys) Closest(
        (Episode Episode, IReadOnlyList<string> Keys)[] conflicts,
        CategorisedItem categorisedItem)
    {
        var (title, release, duration) = categorisedItem.Authority == Service.Apple
            ? (categorisedItem.ResolvedAppleItem?.EpisodeTitle, categorisedItem.ResolvedAppleItem?.Release,
                categorisedItem.ResolvedAppleItem?.Duration)
            : (categorisedItem.ResolvedSpotifyItem?.EpisodeTitle, categorisedItem.ResolvedSpotifyItem?.Release,
                categorisedItem.ResolvedSpotifyItem?.Duration);
        var normalisedTitle = string.IsNullOrWhiteSpace(title) ? null : WebUtility.HtmlDecode(title.Trim());
        return conflicts
            .OrderByDescending(conflict => normalisedTitle == null
                ? 0
                : FuzzyMatcher.Score(normalisedTitle, WebUtility.HtmlDecode(conflict.Episode.Title.Trim())))
            .ThenBy(conflict => release is { } r ? (conflict.Episode.ReleaseUtc - r).Duration() : TimeSpan.Zero)
            .ThenBy(conflict => duration is { } d ? (conflict.Episode.Length - d).Duration() : TimeSpan.Zero)
            .ThenBy(conflict => conflict.Episode.Id)
            .First();
    }

    private static ServiceLinkConflictHold ToHold(
        Episode episode,
        IReadOnlyList<string> keys,
        CategorisedItem categorisedItem) =>
        new(
            episode.Id,
            episode.Title,
            episode.ReleaseUtc,
            keys.Select(key => new ServiceLinkConflictLink(
                    key,
                    EpisodeServicePresence.TryGetUrl(episode, key),
                    key == ServiceKeys.Spotify
                        ? categorisedItem.ResolvedSpotifyItem?.Url
                        : categorisedItem.ResolvedAppleItem?.Url))
                .ToArray());
}
