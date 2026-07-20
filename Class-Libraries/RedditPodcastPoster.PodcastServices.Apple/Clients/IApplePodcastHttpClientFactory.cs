using RedditPodcastPoster.DependencyInjection;

namespace RedditPodcastPoster.PodcastServices.Apple.Clients;

public interface IApplePodcastHttpClientFactory : IAsyncFactory<HttpClient>
{
}