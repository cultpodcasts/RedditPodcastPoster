using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Bluesky.Client;
using RedditPodcastPoster.Bluesky.Configuration;

namespace RedditPodcastPoster.Bluesky.Factories;

public class BlueskyClientFactory(
    IOptions<BlueskyOptions> options,
    ILogger<BlueskyClientFactory> logger,
    ILogger<EmbedCardBlueskyClient> blueskyLogger
) : IBlueskyClientFactory
{
    private readonly BlueskyOptions _options = options.Value;

    public IEmbedCardBlueskyClient Create()
    {
        logger.LogInformation($"Creating blue-sky client with reuse-session: '{_options.ReuseSession}'.");

        return new EmbedCardBlueskyClient(
            _options.Identifier,
            _options.Password,
            _options.ReuseSession,
            blueskyLogger);
    }
}