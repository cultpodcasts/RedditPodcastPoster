namespace RedditPodcastPoster.PodcastServices.Apple;

public interface IApplePodcastResolver
{
    Task<iTunesSearch.Library.Models.Podcast?> FindPodcast(FindApplePodcastRequest request);
}