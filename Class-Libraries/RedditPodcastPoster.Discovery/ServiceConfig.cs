using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Discovery;

public record ServiceConfig(string Term, DiscoverService DiscoverService);