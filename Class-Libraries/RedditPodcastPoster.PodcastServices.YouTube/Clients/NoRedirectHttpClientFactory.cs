using RedditPodcastPoster.PodcastServices.YouTube.Factories;

namespace RedditPodcastPoster.PodcastServices.YouTube.Clients;

public class NoRedirectHttpClientFactory : INoRedirectHttpClientFactory
{
    public HttpClient Create()
    {
        return new HttpClient(new HttpClientHandler {AllowAutoRedirect = false});
    }
}