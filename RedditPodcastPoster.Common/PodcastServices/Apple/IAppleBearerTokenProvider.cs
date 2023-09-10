using System.Net.Http.Headers;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public interface IAppleBearerTokenProvider
{
    Task<AuthenticationHeaderValue> GetHeader();
}