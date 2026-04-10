namespace RedditPodcastPoster.PodcastServices.YouTube.Models;

public record GetPlaylistEpisodesResponse(
    IList<RedditPodcastPoster.Models.Episode>? Results,
    bool IsExpensiveQuery = false);