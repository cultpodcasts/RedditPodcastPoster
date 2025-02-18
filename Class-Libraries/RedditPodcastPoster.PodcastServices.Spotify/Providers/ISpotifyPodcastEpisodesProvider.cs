using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Providers;

public interface ISpotifyPodcastEpisodesProvider : IFlushable
{
    public Task<PodcastEpisodesResult> GetAllEpisodes(
        FindSpotifyEpisodeRequest request,
        IndexingContext indexingContext, 
        string market);

    Task<PodcastEpisodesResult> GetEpisodes(
        GetEpisodesRequest request, 
        IndexingContext indexingContext);
}