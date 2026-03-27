using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.V2;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public interface ISpotifyEpisodeRetrievalHandler
{
    Task<EpisodeRetrievalHandlerResponse> GetEpisodes(Podcast podcast, IndexingContext indexingContext);
}