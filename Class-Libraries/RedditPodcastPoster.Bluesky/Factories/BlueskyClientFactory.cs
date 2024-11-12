using Microsoft.Extensions.Options;
using RedditPodcastPoster.Bluesky.Configuration;
using X.Bluesky;

namespace RedditPodcastPoster.Bluesky.Factories;

public class BlueskyClientFactory(IOptions<BlueskyOptions> options) : IBlueskyClientFactory
{
    private readonly BlueskyOptions _options = options.Value;

    public IBlueskyClient Create()
    {
        return new BlueskyClient(_options.Identifier, _options.Password);
    }
}