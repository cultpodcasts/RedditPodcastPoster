using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Models.Discovery;

public enum DiscoveryResultState
{
    None = 0,
    Unprocessed,
    Rejected,
    Accepted,
    AcceptError
}