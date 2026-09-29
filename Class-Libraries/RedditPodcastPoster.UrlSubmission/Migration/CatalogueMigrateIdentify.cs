using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission.Migration;

/// <summary>
/// Dry-run identify for corpus migrate (Build 5). Apply is GATE 5 and needs an allowlist for News (S-008).
/// Apply batches in GATE 5 are still News then Film then TV (S-007).
/// A single row uses the submit classifier: News first, then series (TV) over Film.
/// </summary>
public static class CatalogueMigrateIdentify
{
    public static CatalogueMigrateSuggestion FromSignals(IEnumerable<SubmitClassificationSignals> signals)
    {
        ArgumentNullException.ThrowIfNull(signals);
        var classified = signals.Select(SubmitContentClassifier.Classify).ToList();
        if (classified.Any(row => row.ContentKind == SubmitClassification.NewsReport && !row.Reject))
        {
            return new CatalogueMigrateSuggestion(
                SubmitClassification.NewsReport,
                RequiresAllowlist: true,
                RequiresCurator: classified.Any(row => row.RequiresCurator));
        }

        if (classified.Any(row => row.ContentKind == SubmitClassification.TvShowEpisode && !row.Reject))
        {
            return new CatalogueMigrateSuggestion(
                SubmitClassification.TvShowEpisode,
                RequiresAllowlist: false,
                RequiresCurator: classified.Any(row => row.RequiresCurator));
        }

        if (classified.Any(row => row.ContentKind == SubmitClassification.Film && !row.Reject))
        {
            return new CatalogueMigrateSuggestion(
                SubmitClassification.Film,
                RequiresAllowlist: false,
                RequiresCurator: classified.Any(row => row.RequiresCurator));
        }

        return new CatalogueMigrateSuggestion(
            SubmitClassification.Episode,
            RequiresAllowlist: false,
            RequiresCurator: classified.Any(row => row.RequiresCurator));
    }

    /// <summary>
    /// Stored-corpus identify: classify from episode service URLs without scraping.
    /// Film and series flags are not on stored episodes, so those rows stay Episode until GATE 5 movers get curator/scrape signals.
    /// </summary>
    public static CatalogueMigrateSuggestion FromEpisodes(IEnumerable<Episode> episodes)
    {
        ArgumentNullException.ThrowIfNull(episodes);
        var signals = episodes.SelectMany(SignalsFromEpisode).ToList();
        if (signals.Count == 0)
        {
            return new CatalogueMigrateSuggestion(
                SubmitClassification.Episode,
                RequiresAllowlist: false,
                RequiresCurator: false);
        }

        return FromSignals(signals);
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
