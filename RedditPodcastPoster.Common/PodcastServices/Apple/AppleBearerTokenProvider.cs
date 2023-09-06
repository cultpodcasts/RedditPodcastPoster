using System.Net.Http.Headers;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public class AppleBearerTokenProvider : IAppleBearerTokenProvider
{
    private readonly string _token;

    public AppleBearerTokenProvider(string token)
    {
        _token = token;
    }

    public AuthenticationHeaderValue GetHeader()
    {
        return new AuthenticationHeaderValue("Bearer", _token);
    }
}