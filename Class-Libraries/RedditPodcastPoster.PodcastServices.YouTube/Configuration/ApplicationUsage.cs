namespace RedditPodcastPoster.PodcastServices.YouTube.Configuration;

[Flags]
public enum ApplicationUsage
{
    Indexer = 1,
    Discover = 2,
    Api = 4,
    Cli = 8,
    Bluesky = 16
}