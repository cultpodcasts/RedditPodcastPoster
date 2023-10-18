using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Spotify;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public interface ISpotifyEpisodeProvider
{
    Task<GetEpisodesResponse> GetEpisodes(GetEpisodesRequest request, IndexingContext indexingContext);
}