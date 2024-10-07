namespace RedditPodcastPoster.CloudflareRedirect;

public interface IRedirectService
{
    public Task<bool> CreatePodcastRedirect(PodcastRedirect podcastRedirect);
}