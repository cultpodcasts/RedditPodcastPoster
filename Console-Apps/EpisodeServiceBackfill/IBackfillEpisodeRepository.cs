using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;

namespace EpisodeServiceBackfill;

/// <summary>
/// CLI-owned surgical Cosmos patch of <c>/services</c> and <c>/ids</c>. Not on the live repository.
/// </summary>
public interface IBackfillEpisodeRepository
{
    Task<bool> PatchServicesAndIds(
        Guid podcastId,
        Guid episodeId,
        Dictionary<string, ServiceLink>? services,
        EpisodeIds? ids);
}
