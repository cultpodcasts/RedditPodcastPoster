namespace RedditPodcastPoster.EntitySearchIndexer;

public interface IEpisodeSearchIndexerService
{
    Task<EntitySearchIndexerResponse> IndexEpisode(Guid episodeId, CancellationToken cancellationToken);
    Task<EntitySearchIndexerResponse> IndexEpisodes(IEnumerable<Guid> episodeIds, CancellationToken cancellationToken);
}