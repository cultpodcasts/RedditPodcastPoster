using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public record PaginateEpisodesResponse(IList<SimpleEpisode> Results, bool IsExpensiveQuery = false);