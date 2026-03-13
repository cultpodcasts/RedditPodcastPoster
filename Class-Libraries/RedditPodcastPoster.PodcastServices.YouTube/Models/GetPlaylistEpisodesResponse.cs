namespace RedditPodcastPoster.PodcastServices.YouTube.Models;

public record GetPlaylistEpisodesResponse(
    IList<RedditPodcastPoster.Models.V2.Episode>? Results,
    bool IsExpensiveQuery = false);