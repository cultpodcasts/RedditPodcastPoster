using RedditPodcastPoster.CloudflareRedirect.Models;

namespace RedditPodcastPoster.CloudflareRedirect.Services;

public interface IRedirectService
{
    public Task<bool> CreatePodcastRedirect(PodcastRedirect podcastRedirect);
}
