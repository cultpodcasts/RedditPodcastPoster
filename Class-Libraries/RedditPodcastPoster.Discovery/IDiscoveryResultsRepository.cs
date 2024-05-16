namespace RedditPodcastPoster.Discovery;

public interface IDiscoveryResultsRepository
{
    Task Save(DiscoveryResultsDocument discoveryResultsDocument);
}