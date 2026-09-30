using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission.Migration;

/// <summary>
/// Dry-run identify for corpus migrate (Build 5). Apply is GATE 5 and needs an allowlist for News (S-008).
/// Apply batches in GATE 5 are still News then Film then TV (S-007).
/// Mixed classified kinds (News plus Episode/Film/TV) stay Episode and need a curator.
/// Homogeneous News still requires every classified URL to be News.
/// Podcast-level dry-run: two or more stored BBC iPlayer episode URLs (TV, count not All),
/// then YouTube-only four-letter names with YouTube-only episodes (S-008 heuristic).
/// Film still needs --podcast-id / scrape flags.
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
    /// Stored-corpus identify from the publisher row plus episodes, without scraping.
    /// URL rules run first. Two or more BBC iPlayer episode URLs with no Spotify/Apple publisher
    /// ids are TV. YouTube-only four-letter names with YouTube-only active episodes are News +
    /// allowlist (S-008 dry-run). TV is checked before the call-sign heuristic.
    /// </summary>
    public static CatalogueMigrateSuggestion FromPodcast(Podcast podcast, IEnumerable<Episode> episodes)
    {
        ArgumentNullException.ThrowIfNull(podcast);
        ArgumentNullException.ThrowIfNull(episodes);
        var active = episodes.Where(episode => !episode.Removed).ToList();
        var fromUrls = FromEpisodes(active);
        if (fromUrls.ContentKind != SubmitClassification.Episode || fromUrls.RequiresCurator)
        {
            return fromUrls;
        }

        if (IsStoredIplayerSeries(podcast, active))
        {
            return new CatalogueMigrateSuggestion(
                SubmitClassification.TvShowEpisode,
                RequiresAllowlist: false,
                RequiresCurator: false);
        }

        if (IsYouTubeOnlyFourLetterNewsStation(podcast, active))
        {
            return new CatalogueMigrateSuggestion(
                SubmitClassification.NewsReport,
                RequiresAllowlist: true,
                RequiresCurator: false);
        }

        return fromUrls;
    }

    /// <summary>
    /// Stored-corpus identify: classify from episode service URLs without scraping.
    /// Film and series flags are not on stored episodes, so those rows stay Episode until GATE 5 movers get curator/scrape signals.
    /// Prefer <see cref="FromPodcast"/> when the publisher row is available.
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

    public static bool IsYouTubeOnlyFourLetterNewsStation(Podcast podcast, IReadOnlyList<Episode> episodes)
    {
        ArgumentNullException.ThrowIfNull(podcast);
        ArgumentNullException.ThrowIfNull(episodes);
        if (episodes.Count == 0)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(podcast.YouTubeChannelId))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(podcast.SpotifyId) || podcast.AppleId is not null)
        {
            return false;
        }

        if (episodes.Any(HasBbcIplayerOrSoundsUrl))
        {
            return false;
        }

        var name = podcast.Name?.Trim() ?? string.Empty;
        return name.Length == 4 && name.All(char.IsAsciiLetter);
    }

    public static bool IsStoredIplayerSeries(Podcast podcast, IReadOnlyList<Episode> episodes)
    {
        ArgumentNullException.ThrowIfNull(podcast);
        ArgumentNullException.ThrowIfNull(episodes);
        if (!string.IsNullOrWhiteSpace(podcast.SpotifyId) || podcast.AppleId is not null)
        {
            return false;
        }

        return episodes.Count(HasBbcIplayerEpisodeUrl) >= 2;
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

    private static bool HasBbcIplayerOrSoundsUrl(Episode episode) =>
        HasBbcIplayerEpisodeUrl(episode) || HasBbcSoundsUrl(episode);

    private static bool HasBbcIplayerEpisodeUrl(Episode episode) =>
        HasServiceUrl(episode, SubmitContentClassifier.IsBbcIplayerEpisode);

    private static bool HasBbcSoundsUrl(Episode episode) =>
        HasServiceUrl(episode, IsBbcSoundsPath);

    private static bool HasServiceUrl(Episode episode, Func<Uri, bool> match)
    {
        if (episode.Services is not { Count: > 0 })
        {
            return false;
        }

        return episode.Services.Values.Any(link => link?.Url is not null && match(link.Url));
    }

    private static bool IsBbcSoundsPath(Uri url)
    {
        if (!url.IsAbsoluteUri)
        {
            return false;
        }

        var host = url.Host;
        var isBbc = host.Equals("bbc.co.uk", StringComparison.OrdinalIgnoreCase)
                    || host.Equals("bbc.com", StringComparison.OrdinalIgnoreCase)
                    || host.EndsWith(".bbc.co.uk", StringComparison.OrdinalIgnoreCase)
                    || host.EndsWith(".bbc.com", StringComparison.OrdinalIgnoreCase);
        return isBbc && url.AbsolutePath.StartsWith("/sounds/", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record CatalogueMigrateSuggestion(
    string ContentKind,
    bool RequiresAllowlist,
    bool RequiresCurator);
