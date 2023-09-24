using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

    public TwitterClient Create()
    {
        throw new NotImplementedException();
    }
}