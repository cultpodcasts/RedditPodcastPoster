using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Models;

public class PodcastEpisodesResult(
    IEnumerable<SimpleEpisode> episodes,
    bool? expensiveQueryFound = null)
{
    public IEnumerable<SimpleEpisode> Episodes => episodes.Where(x => x?.Type == ItemType.Episode);
    /// <summary>
    /// Catalogue-order probe: true = oldest-first (expensive), false = newest-first, null = inconclusive.
    /// </summary>
    public bool? ExpensiveQueryFound { get; } = expensiveQueryFound;
}
