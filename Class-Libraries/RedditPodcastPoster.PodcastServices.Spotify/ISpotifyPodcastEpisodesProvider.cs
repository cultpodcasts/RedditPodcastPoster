using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public interface ISpotifyPodcastEpisodesProvider : IFlushable
{
    public Task<PodcastEpisodesResult> GetAllEpisodes(FindSpotifyEpisodeRequest request,
        IndexingContext indexingContext, string market);

    Task<PodcastEpisodesResult> GetEpisodes(GetEpisodesRequest request, IndexingContext indexingContext);
}