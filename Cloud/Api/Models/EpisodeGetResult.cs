using Episode = RedditPodcastPoster.Models.Episodes.Episode;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;
using Subject = RedditPodcastPoster.Models.Subjects.Subject;

namespace Api.Models;

public enum EpisodeGetStatus
{
    Ok,
    EpisodeNotFound,
    PodcastNotFound,
    Failed
}

public record EpisodeGetResult(
    EpisodeGetStatus Status,
    Episode? Episode = null,
    Podcast? Podcast = null,
    IReadOnlyList<Subject>? Subjects = null);
