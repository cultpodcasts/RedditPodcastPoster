using RedditPodcastPoster.Common.PodcastServices.Spotify;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public interface IApplePodcastResolver
{
    Task<iTunesSearch.Library.Models.Podcast?> FindPodcast(FindApplePodcastRequest request);
}