namespace RedditPodcastPoster.EntitySearchIndexer;

using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;

public interface IEpisodeSearchIndexerService
{
    Task<EntitySearchIndexerResponse> IndexEpisode(Guid episodeId, CancellationToken cancellationToken);

    Task<EntitySearchIndexerResponse> IndexEpisode(
        Podcast podcast,
        Episode episode,
        CancellationToken cancellationToken);

    Task<EntitySearchIndexerResponse> IndexEpisodes(IEnumerable<Guid> episodeIds, CancellationToken cancellationToken);
}