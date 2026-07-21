using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.EntitySearchIndexer.Services;

public interface IEpisodeSearchIndexerService
{
    Task<EntitySearchIndexerResponse> IndexEpisode(Guid episodeId, CancellationToken cancellationToken);

    Task<EntitySearchIndexerResponse> IndexEpisode(
        Podcast podcast,
        Episode episode,
        CancellationToken cancellationToken);

    Task<EntitySearchIndexerResponse> IndexEpisodes(IEnumerable<Guid> episodeIds, CancellationToken cancellationToken);
}
