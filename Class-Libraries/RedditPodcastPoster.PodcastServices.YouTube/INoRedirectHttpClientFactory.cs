namespace RedditPodcastPoster.PodcastServices.YouTube;

public interface INoRedirectHttpClientFactory
{
    HttpClient Create();
}