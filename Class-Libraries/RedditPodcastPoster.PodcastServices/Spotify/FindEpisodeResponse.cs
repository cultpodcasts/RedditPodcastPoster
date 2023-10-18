using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public record FindEpisodeResponse(FullEpisode? FullEpisode, bool IsExpensiveQuery= false);