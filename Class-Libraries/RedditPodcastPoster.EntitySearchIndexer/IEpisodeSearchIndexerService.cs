namespace RedditPodcastPoster.EntitySearchIndexer;

public interface IEpisodeSearchIndexerService
{
    Task IndexEpisode(Guid episodeId, CancellationToken cancellationToken);
    Task IndexEpisodes(IEnumerable<Guid> episodeIds, CancellationToken cancellationToken);
}