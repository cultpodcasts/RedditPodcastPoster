using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using EpisodeServiceBackfill;

namespace RedditPodcastPoster.Persistence.Tests.Fakes;

public sealed class InMemoryBackfillEpisodeRepository(InMemoryEpisodeRepository inner) : IBackfillEpisodeRepository
{
    public Task<bool> PatchServicesAndIds(
        Guid podcastId,
        Guid episodeId,
        Dictionary<string, EpisodeServiceLink>? services,
        EpisodeIds? ids) =>
        inner.PatchServicesAndIds(podcastId, episodeId, services, ids);
}
