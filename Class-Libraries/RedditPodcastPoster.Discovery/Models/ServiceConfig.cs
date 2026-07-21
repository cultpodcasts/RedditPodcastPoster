
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Discovery.Models;

public class ServiceConfig
{
    public required string Term { get; set; }
    public DiscoverService DiscoverService { get; set; }
}