using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Models;

public record FindEpisodeResponse(FullEpisode? FullEpisode, bool IsExpensiveQuery = false);