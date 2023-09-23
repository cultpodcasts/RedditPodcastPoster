using Google.Apis.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tweetinvi;

namespace Indexer.Tweets;

public class TwitterClientFactory : ITwitterClientFactory
{
    private readonly ILogger<TwitterClientFactory> _logger;
    private readonly TwitterOptions _options;

    public TwitterClientFactory(
        IOptions<TwitterOptions> options,
        ILogger<TwitterClientFactory> logger)
    {
        _logger = logger;
        _options = options.Value;
    }

    public ITwitterClient Create()
    {
        return new TwitterClient(
            _options.ConsumerKey, 
            _options.ConsumerSecret, 
            _options.AccessToken,
            _options.AccessTokenSecret);
    }
}