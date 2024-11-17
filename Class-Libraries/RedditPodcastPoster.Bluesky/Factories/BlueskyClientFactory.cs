using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Bluesky.Configuration;
using X.Bluesky;

namespace RedditPodcastPoster.Bluesky.Factories;

public class BlueskyClientFactory(
    IBlueskyApiHttpClientFactory httpClientFactory,
    IOptions<BlueskyOptions> options,
    ILogger<BlueskyClientFactory> logger,
    ILogger<BlueskyClient> blueskyLogger
) : IBlueskyClientFactory
{
    private readonly BlueskyOptions _options = options.Value;

    public IBlueskyClient Create()
    {
        logger.LogInformation($"Creating blue-sky client with reuse-session: '{_options.ReuseSession}'.");

        return new BlueskyClient(
            httpClientFactory,
            _options.Identifier,
            _options.Password,
            ["en", "en-US", "en-GB"],
            _options.ReuseSession,
            blueskyLogger);
    }
}