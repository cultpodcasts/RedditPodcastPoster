using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Bluesky.Client;
using RedditPodcastPoster.Bluesky.Configuration;
using X.Bluesky;

namespace RedditPodcastPoster.Bluesky.Factories;

public class BlueskyClientFactory(
    IOptions<BlueskyOptions> options,
    ILogger<BlueskyClientFactory> logger,
    ILogger<EmbedCardBlueskyClient> blueskyLogger,
    ILogger<BlueskyClient> blueskyClientLogger,
    ILogger<MentionResolver> mentionResolver
) : IBlueskyClientFactory
{
    private readonly BlueskyOptions _options = options.Value;

    public IEmbedCardBlueskyClient Create()
    {
        logger.LogInformation($"Creating blue-sky client with reuse-session: '{_options.ReuseSession}'.");

        return new EmbedCardBlueskyClient(
            new BlueskyHttpClientFactory(),
            _options.Identifier,
            _options.Password,
            ["en", "en-US"],
            _options.ReuseSession,
            blueskyLogger,
            blueskyClientLogger,
            mentionResolver);
    }
}