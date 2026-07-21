using EpisodeModel = RedditPodcastPoster.Models.Episodes.Episode;

namespace RedditPodcastPoster.PodcastServices.YouTube.Models;

public record GetPlaylistEpisodesResponse(
    IList<EpisodeModel>? Results,
    bool IsExpensiveQuery = false);
