namespace RedditPodcastPoster.CloudflareRedirect;

public interface IRedirectService
{
    public Task<CreateRedirectResult> CreatePodcastRedirect(PodcastRedirect podcastRedirect);
    public Task<List<PodcastRedirect>> GetPodcastRedirectChain(PodcastRedirect podcastRedirect);
    public Task<List<PodcastRedirect>> GetAllPodcastRedirects();
}