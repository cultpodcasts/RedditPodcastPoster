namespace RedditPodcastPoster.Models;

public enum DiscoveryResultState
{
    None = 0,
    Unprocessed,
    Rejected,
    Accepted,
    AcceptError
}