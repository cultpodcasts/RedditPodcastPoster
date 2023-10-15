namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public interface ISpotifyEpisodeProvider
{
    Task<GetEpisodesResponse> GetEpisodes(SpotifyPodcastId podcastId, IndexingContext indexingContext);
}