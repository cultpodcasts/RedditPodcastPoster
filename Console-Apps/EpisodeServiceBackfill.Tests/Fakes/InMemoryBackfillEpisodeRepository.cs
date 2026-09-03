using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace EpisodeServiceBackfill.Tests.Fakes;

public sealed class InMemoryBackfillEpisodeRepository(InMemoryEpisodeRepository inner) : IBackfillEpisodeRepository
{
    public Task<bool> PatchServicesAndIds(
        Guid podcastId,
        Guid episodeId,
        Dictionary<string, EpisodeServiceLink>? services,
        EpisodeIds? ids)
    {
        var written = inner.TryMutate(podcastId, episodeId, episode =>
        {
            if (services is { Count: > 0 })
            {
                episode.Services = services.ToDictionary(
                    x => x.Key,
                    x => new EpisodeServiceLink { Url = x.Value.Url, Image = x.Value.Image },
                    StringComparer.Ordinal);
            }

            if (ids is not null && !ids.IsEmpty)
            {
                episode.Ids = new EpisodeIds
                {
                    Spotify = ids.Spotify,
                    Apple = ids.Apple,
                    YouTube = ids.YouTube
                };
            }
        });
        return Task.FromResult(written);
    }
}
