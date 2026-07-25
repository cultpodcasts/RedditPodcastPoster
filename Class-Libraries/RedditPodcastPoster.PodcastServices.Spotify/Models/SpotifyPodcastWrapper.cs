using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Models;

public class SpotifyPodcastWrapper
{
    public SpotifyPodcastWrapper(FullShow? fullShow = null, SimpleShow? simpleShow = null,
        bool? expensiveQueryFound = null)
    {
        FullShow = fullShow;
        SimpleShow = simpleShow;
        ExpensiveQueryFound = expensiveQueryFound;
    }

    public string Id => FullShow?.Id ?? SimpleShow?.Id ?? string.Empty;

    public FullShow? FullShow { get; init; }
    public SimpleShow? SimpleShow { get; init; }
    public bool? ExpensiveQueryFound { get; init; }
}
