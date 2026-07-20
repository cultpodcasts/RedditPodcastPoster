using RedditPodcastPoster.PodcastServices.Apple.Models;

namespace RedditPodcastPoster.PodcastServices.Apple.Resolvers;

public interface IApplePodcastResolver
{
    Task<iTunesSearch.Library.Models.Podcast?> FindPodcast(FindApplePodcastRequest request);
}