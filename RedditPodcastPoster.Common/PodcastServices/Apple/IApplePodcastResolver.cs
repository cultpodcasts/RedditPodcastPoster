using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public interface IApplePodcastResolver
{
    Task<iTunesSearch.Library.Models.Podcast?> FindPodcast(Podcast podcast);
}