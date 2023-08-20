using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public interface ISpotifyPodcastEnricher
{
    Task<bool> AddIdAndUrls(Podcast podcast);
}