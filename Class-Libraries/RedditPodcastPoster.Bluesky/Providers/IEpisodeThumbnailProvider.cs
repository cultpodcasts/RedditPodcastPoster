using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Bluesky.Providers;

public interface IEpisodeThumbnailProvider
{
    Task<Uri?> GetThumbnail(PodcastEpisode podcastEpisode, Service urlService);
}