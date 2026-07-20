using System.Net.Http.Headers;

namespace RedditPodcastPoster.PodcastServices.Apple.Clients;

public interface IAppleBearerTokenProvider
{
    Task<AuthenticationHeaderValue> GetHeader();
}