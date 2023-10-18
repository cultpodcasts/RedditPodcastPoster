using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public interface ISpotifyEpisodeProvider
{
    Task<GetEpisodesResponse> GetEpisodes(
        GetEpisodesRequest request, 
        IndexingContext indexingContext);
}