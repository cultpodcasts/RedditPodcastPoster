using RedditPodcastPoster.Models.V2;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public interface IEpisodeRetrievalHandler
{
    Task<EpisodeRetrievalHandlerResponse> GetEpisodes(Podcast podcast, IEnumerable<Episode> episodes,
        IndexingContext indexingContext);
}