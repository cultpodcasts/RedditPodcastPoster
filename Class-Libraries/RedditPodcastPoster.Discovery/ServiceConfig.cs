using DiscoverService = RedditPodcastPoster.Models.DiscoverService;

namespace RedditPodcastPoster.Discovery;

public class ServiceConfig
{
    public required string Term { get; set; }
    public DiscoverService DiscoverService { get; set; }
}