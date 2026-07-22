using Episode = RedditPodcastPoster.Models.Episodes.Episode;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace Api.Models;

public enum PublicEpisodeGetStatus
{
    Ok,
    NotFound,
    Failed
}

public record PublicEpisodeGetResult(
    PublicEpisodeGetStatus Status,
    Episode? Episode = null,
    Podcast? Podcast = null);
