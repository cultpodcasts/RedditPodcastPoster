namespace RedditPodcastPoster.PodcastServices.Apple;

public interface IApplePodcastHttpClientFactory
{
    Task<HttpClient> Create();
}