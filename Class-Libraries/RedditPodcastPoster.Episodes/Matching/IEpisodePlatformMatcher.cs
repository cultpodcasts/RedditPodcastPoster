using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Matching;

public interface IEpisodePlatformMatcher
{
    bool IsMatch(
        Episode existingEpisode,
        Episode incomingEpisode,
        Regex? episodeMatchRegex,
        Podcast podcast,
        IReadOnlyList<Episode> existingEpisodes);

    /// <summary>
    /// Same decision as <see cref="IsMatch"/>, plus how it was reached: <see cref="EpisodeMatchKind.Certain"/>
    /// (same platform id, exact title, or the podcast's EpisodeMatchRegex) or <see cref="EpisodeMatchKind.Heuristic"/>
    /// (fuzzy title + duration, or release + duration).
    /// </summary>
    EpisodeMatchKind Classify(
        Episode existingEpisode,
        Episode incomingEpisode,
        Regex? episodeMatchRegex,
        Podcast podcast,
        IReadOnlyList<Episode> existingEpisodes);

    Episode? FindCatalogueMatchByLength(
        Episode probe,
        IEnumerable<Episode> candidates,
        Podcast podcast,
        Regex? episodeMatchRegex,
        CatalogueMatchByLengthOptions options,
        Func<Episode, bool>? reducer = null);

    Episode? FindCatalogueMatchByDate(
        Episode probe,
        IEnumerable<Episode> candidates,
        Podcast podcast,
        Regex? episodeMatchRegex,
        Func<Episode, bool>? reducer = null);

    bool CatalogueReleaseMatches(Episode probe, Episode catalogueItem, Podcast podcast);

    bool IsCatalogueMatch(
        Episode probe,
        Episode catalogueItem,
        Podcast podcast,
        Regex? episodeMatchRegex);
}
