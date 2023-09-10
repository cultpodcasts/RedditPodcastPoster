using iTunesSearch.Library.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public interface IApplePodcastResolver
{
    Task<Podcast?> FindPodcast(FindApplePodcastRequest request);
}