namespace RedditPodcastPoster.PodcastServices.YouTube.Factories;

public interface INoRedirectHttpClientFactory
{
    HttpClient Create();
}