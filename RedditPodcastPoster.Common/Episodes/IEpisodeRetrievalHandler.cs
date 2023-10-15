using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public interface IEpisodeRetrievalHandler
{
    Task<EpisodeRetrievalHandlerResponse> GetEpisodes(Podcast podcast, IndexingContext indexingContext,
        IList<Episode> episodes);
}