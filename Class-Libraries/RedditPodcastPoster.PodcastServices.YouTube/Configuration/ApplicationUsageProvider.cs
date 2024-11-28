namespace RedditPodcastPoster.PodcastServices.YouTube.Configuration;

public class ApplicationUsageProvider(ApplicationUsage usage) : IApplicationUsageProvider
{
    public ApplicationUsage GetApplicationUsage() => usage;
}