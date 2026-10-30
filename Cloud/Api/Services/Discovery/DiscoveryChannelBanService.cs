using Api.Models;
using Api.Services.Podcasts;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Catalogue.Podcasts;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Services;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace Api.Services.Discovery;

/// <summary>
/// Bans the show behind one discovery result: an existing podcast is marked removed, and a channel that has never
/// been catalogued becomes a minimal removed podcast. Sibling cards that share a YouTube channel, Spotify show, or
/// Apple podcast id are linked to that podcast so the next discovery GET shows the removed icon. A shared title is
/// not identity. Several catalogue matches, with no single card id, are a conflict and write nothing. The command
/// does not return the podcast.
/// </summary>
public class DiscoveryChannelBanService(
    IDiscoveryResultsRepository discoveryResultsRepository,
    IPodcastRepository podcastRepository,
    IPodcastFactory podcastFactory,
    IPodcastUpdateService podcastUpdateService,
    IPodcastService podcastService,
    INewsOrganisationRepository newsOrganisationRepository,
    ITvShowRepository tvShowRepository,
    ILogger<DiscoveryChannelBanService> logger) : IDiscoveryChannelBanService
{
    public Task<DiscoveryChannelBanResult> BanAsync(Guid resultId, CancellationToken cancellationToken) =>
        BanCoreAsync(resultId, cancellationToken);

    private async Task<DiscoveryChannelBanResult> BanCoreAsync(Guid resultId, CancellationToken cancellationToken)
    {
        var documents = await discoveryResultsRepository.GetAllUnprocessed().ToListAsync(cancellationToken);
        DiscoveryResult? result = null;
        foreach (var document in documents)
        {
            result = document.DiscoveryResults.FirstOrDefault(x => x.Id == resultId);
            if (result != null)
            {
                break;
            }
        }

        if (result == null)
        {
            return new DiscoveryChannelBanResult(DiscoveryChannelBanStatus.NotFound,
                "Discovery result was not found in the current queue.");
        }

        var identity = await ResolveIdentity(result, cancellationToken);
        if (identity.IsEmpty)
        {
            return new DiscoveryChannelBanResult(DiscoveryChannelBanStatus.BadRequest,
                "This discovery result has no YouTube channel, Spotify show, or Apple podcast id to remove.");
        }

        ApplyIdentity(result, identity);
        var podcasts = await FindPodcasts(identity, cancellationToken);
        var chosen = Choose(podcasts, result.MatchingPodcastIds);
        if (podcasts.Select(x => x.Id).Distinct().Count() > 1 && chosen == null)
        {
            return new DiscoveryChannelBanResult(DiscoveryChannelBanStatus.Conflict,
                "More than one podcast matches this channel.");
        }

        var linked = new List<(DiscoveryResultsDocument Document, DiscoveryResult Candidate)>();
        foreach (var document in documents)
        {
            foreach (var candidate in document.DiscoveryResults)
            {
                if (await SameChannel(candidate, result, identity, cancellationToken))
                {
                    linked.Add((document, candidate));
                }
            }
        }

        Podcast podcast;
        if (chosen != null)
        {
            await FillMissingIds(chosen, identity, cancellationToken);
            var update = await podcastUpdateService.UpdateAsync(
                new PodcastChangeRequestWrapper(chosen.Id, new PodcastChangeRequest { Removed = true }),
                cancellationToken);
            if (update.Status != PodcastUpdateStatus.Accepted)
            {
                logger.LogError("Removing podcast '{PodcastId}' failed with status {Status}.", chosen.Id, update.Status);
                return new DiscoveryChannelBanResult(DiscoveryChannelBanStatus.Failed, "Unable to remove the podcast.");
            }

            podcast = chosen;
        }
        else
        {
            if (await ParentOwns(identity, cancellationToken))
            {
                return new DiscoveryChannelBanResult(DiscoveryChannelBanStatus.Conflict,
                    "A news organisation or TV show already owns this channel.");
            }

            var name = FirstName(result.ShowName, identity.YouTubeChannelTitle);
            if (name == null)
            {
                return new DiscoveryChannelBanResult(DiscoveryChannelBanStatus.BadRequest,
                    "This discovery result has no show name to create a removed podcast from.");
            }

            podcast = await podcastFactory.Create(name);
            podcast.YouTubeChannelId = identity.YouTubeChannelId ?? string.Empty;
            podcast.SpotifyId = identity.SpotifyShowId ?? string.Empty;
            podcast.AppleId = identity.ApplePodcastId;
            podcast.Removed = true;
            await podcastRepository.Save(podcast);
            logger.LogInformation(
                "Created removed podcast '{PodcastId}' for discovery result '{ResultId}'.", podcast.Id, result.Id);
        }

        var dirty = false;
        foreach (var group in linked.GroupBy(x => x.Document))
        {
            foreach (var (_, candidate) in group)
            {
                ApplyIdentity(candidate, identity);
                LinkPodcast(candidate, podcast.Id);
            }

            await discoveryResultsRepository.Save(group.Key);
            dirty = true;
        }

        if (!dirty)
        {
            logger.LogWarning("Ban of discovery result '{ResultId}' did not stamp any open report.", result.Id);
        }

        return new DiscoveryChannelBanResult(DiscoveryChannelBanStatus.Accepted);
    }

    private async Task<EpisodeUrlOwnerIds> ResolveIdentity(DiscoveryResult result, CancellationToken cancellationToken)
    {
        var channel = NullIfEmpty(result.YouTubeChannelId);
        var spotify = NullIfEmpty(result.SpotifyId);
        var apple = result.AppleId;
        string? channelTitle = null;
        var context = new IndexingContext(
            SkipYouTubeUrlResolving: false,
            SkipSpotifyUrlResolving: false,
            SkipExpensiveYouTubeQueries: false,
            SkipPodcastDiscovery: false,
            SkipExpensiveSpotifyQueries: false)
        {
            LookupFlow = EpisodeLookupFlow.Discovery
        };

        foreach (var url in UrlsStillNeeded(result, channel, spotify, apple))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var owner = await podcastService.GetOwnerIds(url, context);
                channel ??= NullIfEmpty(owner.YouTubeChannelId);
                spotify ??= NullIfEmpty(owner.SpotifyShowId);
                apple ??= owner.ApplePodcastId;
                channelTitle ??= NullIfEmpty(owner.YouTubeChannelTitle);
            }
            catch (Exception ex) when (ex is ArgumentException or HttpRequestException or InvalidOperationException)
            {
                logger.LogWarning(ex,
                    "Could not resolve show ids for discovery result '{ResultId}'.", result.Id);
            }
        }

        return new EpisodeUrlOwnerIds(spotify, apple, channel, channelTitle);
    }

    private static IEnumerable<Uri> UrlsStillNeeded(
        DiscoveryResult result, string? channel, string? spotify, long? apple)
    {
        if (channel == null && result.Urls.YouTube != null)
        {
            yield return result.Urls.YouTube;
        }

        if (spotify == null && result.Urls.Spotify != null)
        {
            yield return result.Urls.Spotify;
        }

        if (apple == null && result.Urls.Apple != null)
        {
            yield return result.Urls.Apple;
        }
    }

    private async Task<List<Podcast>> FindPodcasts(EpisodeUrlOwnerIds identity, CancellationToken cancellationToken)
    {
        var found = new List<Podcast>();
        if (identity.YouTubeChannelId is { } channel)
        {
            found.AddRange(await podcastRepository.GetAllBy(x => x.YouTubeChannelId == channel)
                .ToListAsync(cancellationToken));
        }

        if (identity.SpotifyShowId is { } spotify)
        {
            found.AddRange(await podcastRepository.GetAllBy(x => x.SpotifyId == spotify)
                .ToListAsync(cancellationToken));
        }

        if (identity.ApplePodcastId is { } apple)
        {
            found.AddRange(await podcastRepository.GetAllBy(x => x.AppleId == apple)
                .ToListAsync(cancellationToken));
        }

        return found;
    }

    /// <summary>
    /// The sole id match, or the only match the card already names. Several remaining podcasts are a conflict:
    /// a playlist-scoped show and the channel-wide outlet are not the same podcast.
    /// </summary>
    private static Podcast? Choose(IReadOnlyList<Podcast> podcasts, Guid[]? matchingPodcastIds)
    {
        var distinct = podcasts.DistinctBy(x => x.Id).ToArray();
        if (distinct.Length == 0)
        {
            return null;
        }

        if (distinct.Length == 1)
        {
            return distinct[0];
        }

        if (matchingPodcastIds is not { Length: > 0 })
        {
            return null;
        }

        var named = distinct.Where(x => matchingPodcastIds.Contains(x.Id)).ToArray();
        return named.Length == 1 ? named[0] : null;
    }

    private async Task FillMissingIds(Podcast podcast, EpisodeUrlOwnerIds identity, CancellationToken cancellationToken)
    {
        var changed = false;
        if (string.IsNullOrWhiteSpace(podcast.YouTubeChannelId) && identity.YouTubeChannelId != null)
        {
            podcast.YouTubeChannelId = identity.YouTubeChannelId;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(podcast.SpotifyId) && identity.SpotifyShowId != null)
        {
            podcast.SpotifyId = identity.SpotifyShowId;
            changed = true;
        }

        if (podcast.AppleId == null && identity.ApplePodcastId != null)
        {
            podcast.AppleId = identity.ApplePodcastId;
            changed = true;
        }

        if (changed)
        {
            await podcastRepository.Save(podcast);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private async Task<bool> ParentOwns(EpisodeUrlOwnerIds identity, CancellationToken cancellationToken)
    {
        if (identity.YouTubeChannelId is { } channel)
        {
            if (await newsOrganisationRepository
                    .GetAllBy(x => x.Indexing != null && x.Indexing.YouTubeChannelId == channel)
                    .AnyAsync(cancellationToken))
            {
                return true;
            }

            if (await tvShowRepository
                    .GetAllBy(x => x.Indexing != null && x.Indexing.YouTubeChannelId == channel)
                    .AnyAsync(cancellationToken))
            {
                return true;
            }
        }

        if (identity.SpotifyShowId is { } spotify)
        {
            if (await newsOrganisationRepository
                    .GetAllBy(x => x.Indexing != null && x.Indexing.SpotifyId == spotify)
                    .AnyAsync(cancellationToken))
            {
                return true;
            }

            if (await tvShowRepository
                    .GetAllBy(x => x.Indexing != null && x.Indexing.SpotifyId == spotify)
                    .AnyAsync(cancellationToken))
            {
                return true;
            }
        }

        if (identity.ApplePodcastId is { } apple)
        {
            if (await newsOrganisationRepository
                    .GetAllBy(x => x.Indexing != null && x.Indexing.AppleId == apple)
                    .AnyAsync(cancellationToken))
            {
                return true;
            }

            if (await tvShowRepository
                    .GetAllBy(x => x.Indexing != null && x.Indexing.AppleId == apple)
                    .AnyAsync(cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> SameChannel(
        DiscoveryResult candidate,
        DiscoveryResult source,
        EpisodeUrlOwnerIds identity,
        CancellationToken cancellationToken)
    {
        if (candidate.Id == source.Id)
        {
            return true;
        }

        var candidateIds = StoredIds(candidate);
        if (candidateIds.IsEmpty && HasEpisodeUrl(candidate))
        {
            candidateIds = await ResolveIdentity(candidate, cancellationToken);
        }

        return SharesServiceId(candidateIds, identity);
    }

    private static EpisodeUrlOwnerIds StoredIds(DiscoveryResult result) =>
        new(NullIfEmpty(result.SpotifyId), result.AppleId, NullIfEmpty(result.YouTubeChannelId));

    private static bool HasEpisodeUrl(DiscoveryResult result) =>
        result.Urls.YouTube != null || result.Urls.Spotify != null || result.Urls.Apple != null;

    private static bool SharesServiceId(EpisodeUrlOwnerIds candidate, EpisodeUrlOwnerIds identity)
    {
        if (identity.YouTubeChannelId != null &&
            string.Equals(candidate.YouTubeChannelId, identity.YouTubeChannelId, StringComparison.Ordinal))
        {
            return true;
        }

        if (identity.SpotifyShowId != null &&
            string.Equals(candidate.SpotifyShowId, identity.SpotifyShowId, StringComparison.Ordinal))
        {
            return true;
        }

        return identity.ApplePodcastId != null && candidate.ApplePodcastId == identity.ApplePodcastId;
    }

    private static void LinkPodcast(DiscoveryResult candidate, Guid podcastId)
    {
        var ids = candidate.MatchingPodcastIds ?? [];
        if (!ids.Contains(podcastId))
        {
            candidate.MatchingPodcastIds = [.. ids, podcastId];
        }
    }

    private static void ApplyIdentity(DiscoveryResult result, EpisodeUrlOwnerIds identity)
    {
        result.YouTubeChannelId ??= identity.YouTubeChannelId;
        result.SpotifyId ??= identity.SpotifyShowId;
        result.AppleId ??= identity.ApplePodcastId;
    }

    private static string? FirstName(string? showName, string? channelTitle)
    {
        if (!string.IsNullOrWhiteSpace(showName))
        {
            return showName.Trim();
        }

        return string.IsNullOrWhiteSpace(channelTitle) ? null : channelTitle.Trim();
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
