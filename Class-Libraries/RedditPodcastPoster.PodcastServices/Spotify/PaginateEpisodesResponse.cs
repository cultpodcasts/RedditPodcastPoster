using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public record PaginateEpisodesResponse(IList<SimpleEpisode> Results, bool IsExpensiveQuery = false);