using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public interface ISpotifyEpisodeResolver
{
    Task<FindEpisodeResponse> FindEpisode(FindSpotifyEpisodeRequest request, IndexingContext indexingContext);
    Task<PaginateEpisodesResponse> GetEpisodes(GetEpisodesRequest request, IndexingContext indexingContext);
}