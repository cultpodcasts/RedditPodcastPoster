using RedditPodcastPoster.DependencyInjection;

namespace RedditPodcastPoster.PodcastServices.Apple;

public interface IApplePodcastHttpClientFactory : IAsyncFactory<HttpClient>
{
}