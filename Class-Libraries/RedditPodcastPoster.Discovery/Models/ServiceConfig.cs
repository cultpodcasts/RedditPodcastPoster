
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Discovery;

namespace RedditPodcastPoster.Discovery.Models;

public class ServiceConfig
{
    public required string Term { get; set; }
    public DiscoverService DiscoverService { get; set; }
}