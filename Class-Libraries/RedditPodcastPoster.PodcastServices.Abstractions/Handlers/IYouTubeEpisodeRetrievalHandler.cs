using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Handlers;

public interface IYouTubeEpisodeRetrievalHandler
{
    Task<EpisodeRetrievalHandlerResponse> GetEpisodes(Podcast podcast, IEnumerable<Episode> episodes,
        IndexingContext indexingContext);
}