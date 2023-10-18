using System.Net.Http.Headers;

namespace RedditPodcastPoster.PodcastServices.Apple;

public interface IAppleBearerTokenProvider
{
    Task<AuthenticationHeaderValue> GetHeader();
}