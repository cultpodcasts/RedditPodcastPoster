using System.Text.RegularExpressions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.Matching;

public interface IEpisodePlatformMatcher
{
    bool IsMatch(
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
