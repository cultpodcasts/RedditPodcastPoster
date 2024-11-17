using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Bluesky.HttpHandlers;

namespace RedditPodcastPoster.Bluesky.Factories;

public class BlueskyApiHttpClientFactoryFactory(
    ILogger<LoggingHandler> logger
) : IBlueskyApiHttpClientFactoryFactory
{
    public IBlueskyApiHttpClientFactory Create()
    {
        return new BlueskyApiHttpClientFactory(logger);
    }
}