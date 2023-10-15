using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public record FindEpisodeResponse(FullEpisode? FullEpisode, bool IsExpensiveQuery= false);