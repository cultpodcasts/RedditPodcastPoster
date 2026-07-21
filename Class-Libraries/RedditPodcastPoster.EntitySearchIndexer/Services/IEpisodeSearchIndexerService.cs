using RedditPodcastPoster.EntitySearchIndexer.Models;

namespace RedditPodcastPoster.EntitySearchIndexer.Services;

using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

public interface IEpisodeSearchIndexerService
{
    Task<EntitySearchIndexerResponse> IndexEpisode(Guid episodeId, CancellationToken cancellationToken);

    Task<EntitySearchIndexerResponse> IndexEpisode(
        Podcast podcast,
        Episode episode,
        CancellationToken cancellationToken);

    Task<EntitySearchIndexerResponse> IndexEpisodes(IEnumerable<Guid> episodeIds, CancellationToken cancellationToken);
}
