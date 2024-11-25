namespace RedditPodcastPoster.PodcastServices.YouTube.Configuration;

[Flags]
public enum ApplicationUsage
{
    Indexer = 1,
    Discover,
    Api,
    Cli
}