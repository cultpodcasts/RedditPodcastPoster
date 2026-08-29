using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace EpisodeServiceBackfill;

/// <summary>
/// CLI-owned surgical Cosmos patch of <c>/services</c> and <c>/ids</c>. Not on the live repository.
/// </summary>
public interface IBackfillEpisodeRepository
{
    Task<bool> PatchServicesAndIds(
        Guid podcastId,
        Guid episodeId,
        Dictionary<string, EpisodeServiceLink>? services,
        EpisodeIds? ids);
}
