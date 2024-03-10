namespace RedditPodcastPoster.PodcastServices.YouTube;

public class NoRedirectHttpClientFactory : INoRedirectHttpClientFactory
{
    public HttpClient Create()
    {
        return new HttpClient(new HttpClientHandler {AllowAutoRedirect = false});
    }
}