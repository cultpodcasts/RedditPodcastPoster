namespace RedditPodcastPoster.EntitySearchIndexer;

public interface IEpisodeSearchIndexerService
{
    Task<bool> IndexEpisode(Guid episodeId, CancellationToken cancellationToken);
    Task<bool> IndexEpisodes(IEnumerable<Guid> episodeIds, CancellationToken cancellationToken);
}