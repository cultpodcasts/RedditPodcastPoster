using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Bluesky.Configuration;
using X.Bluesky;

namespace RedditPodcastPoster.Bluesky.Factories;

public class BlueskyClientFactory(
    IOptions<BlueskyOptions> options,
    ILogger<BlueskyClientFactory> logger,
    ILogger<BlueskyClient> blueskyClientLogger
) : IBlueskyClientFactory
{
    private readonly BlueskyOptions _options = options.Value;

    public IBlueskyClient Create()
    {
        var uri = new Uri("https://bsky.social");
        return new BlueskyClient(_options.Identifier, _options.Password, logger: blueskyClientLogger);
    }
}