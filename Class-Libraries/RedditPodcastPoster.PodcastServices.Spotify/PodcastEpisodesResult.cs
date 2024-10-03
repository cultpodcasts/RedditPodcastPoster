using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public record PodcastEpisodesResult(IEnumerable<SimpleEpisode> Episodes, bool ExpensiveQueryFound = false);