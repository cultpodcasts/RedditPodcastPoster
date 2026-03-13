using RedditPodcastPoster.Models.V2;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public interface IYouTubeEpisodeRetrievalHandler
{
    Task<EpisodeRetrievalHandlerResponse> GetEpisodes(Podcast podcast, IEnumerable<Episode> episodes,
        IndexingContext indexingContext);
}