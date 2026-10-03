using RedditPodcastPoster.Models.TvShows;

namespace Api.Models;

public enum TvShowEpisodeGetStatus
{
    Found,
    NotFound,
    Failed
}

public record TvShowEpisodeGetResult(
    TvShowEpisodeGetStatus Status,
    TvShowEpisode? Episode = null);
