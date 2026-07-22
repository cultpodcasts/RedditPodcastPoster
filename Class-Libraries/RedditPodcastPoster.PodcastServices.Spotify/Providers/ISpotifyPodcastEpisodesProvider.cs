using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Providers;

public interface ISpotifyPodcastEpisodesProvider
{
    public Task<PodcastEpisodesResult> GetAllEpisodes(
        FindSpotifyEpisodeRequest request,
        IndexingContext indexingContext, 
        string market);

    Task<PodcastEpisodesResult> GetEpisodes(
        GetEpisodesRequest request, 
        IndexingContext indexingContext);
}
