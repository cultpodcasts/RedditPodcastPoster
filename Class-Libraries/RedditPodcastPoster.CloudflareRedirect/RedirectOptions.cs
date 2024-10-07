namespace RedditPodcastPoster.CloudflareRedirect;

public class RedirectOptions
{
    public required string PodcastRedirectRulesId { get; set; }

    public required Uri PodcastBasePath { get; set; }
}