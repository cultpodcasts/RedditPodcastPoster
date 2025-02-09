using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Providers;

public interface ISpotifyEpisodeProvider
{
    Task<GetEpisodesResponse> GetEpisodes(
        GetEpisodesRequest request, 
        IndexingContext indexingContext);
}