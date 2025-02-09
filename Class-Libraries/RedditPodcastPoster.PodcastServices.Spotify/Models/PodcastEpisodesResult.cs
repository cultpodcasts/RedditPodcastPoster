using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Models;

public class PodcastEpisodesResult(
    IEnumerable<SimpleEpisode> episodes,
    bool expensiveQueryFound = false)
{
    public IEnumerable<SimpleEpisode> Episodes => episodes.Where(x => x?.Type == ItemType.Episode);
    public bool ExpensiveQueryFound { get; } = expensiveQueryFound;
}