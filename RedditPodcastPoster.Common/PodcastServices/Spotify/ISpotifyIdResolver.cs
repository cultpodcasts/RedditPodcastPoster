using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public interface ISpotifyIdResolver
{
    Task<string> FindPodcastId(Podcast podcast, IndexOptions indexOptions);
    Task<string> FindEpisodeId(Podcast podcast, Episode episode, IndexOptions indexOptions);
}