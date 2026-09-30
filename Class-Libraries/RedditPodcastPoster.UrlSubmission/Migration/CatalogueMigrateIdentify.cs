using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission.Migration;

/// <summary>
/// Dry-run identify for corpus migrate (Build 5). Apply is GATE 5 and needs an allowlist for News (S-008).
/// Apply batches in GATE 5 are still News then Film then TV (S-007).
/// Mixed classified kinds (News plus Episode/Film/TV) stay Episode and need a curator.
/// Homogeneous News still requires every classified URL to be News.
/// </summary>
public static class CatalogueMigrateIdentify
{
    public static CatalogueMigrateSuggestion FromSignals(IEnumerable<SubmitClassificationSignals> signals)
    {
        ArgumentNullException.ThrowIfNull(signals);
        var classified = signals.Select(SubmitContentClassifier.Classify).ToList();
        var kinds = classified
            .Where(row => !row.Reject)
            .Select(row => row.ContentKind)
            .Distinct()
            .ToList();
        var requiresCurator = classified.Any(row => row.RequiresCurator);

        if (kinds.Count > 1)
        {
            return new CatalogueMigrateSuggestion(
                SubmitClassification.Episode,
                RequiresAllowlist: false,
                RequiresCurator: true);
        }

        if (kinds.Count == 1 && kinds[0] == SubmitClassification.NewsReport)
        {
            return new CatalogueMigrateSuggestion(
                SubmitClassification.NewsReport,
                RequiresAllowlist: true,
                RequiresCurator: requiresCurator);
        }

        if (kinds.Count == 1 && kinds[0] == SubmitClassification.TvShowEpisode)
        {
            return new CatalogueMigrateSuggestion(
                SubmitClassification.TvShowEpisode,
                RequiresAllowlist: false,
                RequiresCurator: requiresCurator);
        }

        if (kinds.Count == 1 && kinds[0] == SubmitClassification.Film)
        {
            return new CatalogueMigrateSuggestion(
                SubmitClassification.Film,
                RequiresAllowlist: false,
                RequiresCurator: requiresCurator);
        }

        return new CatalogueMigrateSuggestion(
            SubmitClassification.Episode,
            RequiresAllowlist: false,
            RequiresCurator: requiresCurator);
    }

    /// <summary>
    /// Stored-corpus identify: classify from episode service URLs without scraping.
    /// Film and series flags are not on stored episodes, so those rows stay Episode until GATE 5 movers get curator/scrape signals.
    /// </summary>
    public static CatalogueMigrateSuggestion FromEpisodes(IEnumerable<Episode> episodes)
    {
        ArgumentNullException.ThrowIfNull(episodes);
        var active = episodes.ToList();
        var signals = active.SelectMany(SignalsFromEpisode).ToList();
        if (signals.Count == 0)
        {
            return new CatalogueMigrateSuggestion(
                SubmitClassification.Episode,
                RequiresAllowlist: false,
                RequiresCurator: false);
        }

        if (HasMixedNewsAndNonNewsEpisodes(active))
        {
            return new CatalogueMigrateSuggestion(
                SubmitClassification.Episode,
                RequiresAllowlist: false,
                RequiresCurator: true);
        }

        return FromSignals(signals);
    }

    /// <summary>
    /// Stored News station only when every active episode classifies as NewsReport.
    /// A stray BBC <c>/news/</c> URL next to Spotify (or an episode with no News URLs) is mixed.
    /// </summary>
    public static bool IsNewsReportEpisode(Episode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        var signals = SignalsFromEpisode(episode).ToList();
        if (signals.Count == 0)
        {
            return false;
        }

        var suggestion = FromSignals(signals);
        return suggestion.ContentKind == SubmitClassification.NewsReport && !suggestion.RequiresCurator;
    }

    public static IReadOnlyList<Guid> NonNewsEpisodeIds(IEnumerable<Episode> episodes)
    {
        ArgumentNullException.ThrowIfNull(episodes);
        return episodes
            .Where(episode => !IsNewsReportEpisode(episode))
            .Select(episode => episode.Id)
            .ToList();
    }

    private static bool HasMixedNewsAndNonNewsEpisodes(IReadOnlyList<Episode> episodes)
    {
        var newsCount = 0;
        var nonNewsCount = 0;
        foreach (var episode in episodes)
        {
            if (IsNewsReportEpisode(episode))
            {
                newsCount++;
            }
            else
            {
                nonNewsCount++;
            }
        }

        return newsCount > 0 && nonNewsCount > 0;
    }

    private static IEnumerable<SubmitClassificationSignals> SignalsFromEpisode(Episode episode)
    {
        if (episode.Services is not { Count: > 0 })
        {
            yield break;
        }

        foreach (var (key, link) in episode.Services)
        {
            if (link?.Url is null)
            {
                continue;
            }

            var podcastServiceEpisode = key is ServiceKeys.Spotify or ServiceKeys.Apple or ServiceKeys.YouTube;
            yield return new SubmitClassificationSignals(
                link.Url,
                PodcastServiceEpisode: podcastServiceEpisode);
        }
    }
}

public sealed record CatalogueMigrateSuggestion(
    string ContentKind,
    bool RequiresAllowlist,
    bool RequiresCurator);
