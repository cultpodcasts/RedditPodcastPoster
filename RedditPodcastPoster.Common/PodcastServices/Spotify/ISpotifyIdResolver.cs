using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public interface ISpotifyIdResolver
{
    Task<string> FindPodcastId(Podcast podcast, IndexingContext indexingContext);
    Task<string> FindEpisodeId(Podcast podcast, Episode episode, IndexingContext indexingContext);
}