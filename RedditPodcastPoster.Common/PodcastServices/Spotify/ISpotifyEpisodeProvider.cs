using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public interface ISpotifyEpisodeProvider
{
    Task<IList<Episode>?> GetEpisodes(SpotifyPodcastId podcastId, IndexingContext indexingContext);
}