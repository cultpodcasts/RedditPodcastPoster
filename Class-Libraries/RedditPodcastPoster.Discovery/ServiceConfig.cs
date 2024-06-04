using DiscoverService = RedditPodcastPoster.Models.DiscoverService;

namespace RedditPodcastPoster.Discovery;

public record ServiceConfig(string Term, DiscoverService DiscoverService);