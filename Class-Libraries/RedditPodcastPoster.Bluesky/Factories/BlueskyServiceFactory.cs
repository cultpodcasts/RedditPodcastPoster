using idunno.Bluesky;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Bluesky.Configuration;

namespace RedditPodcastPoster.Bluesky.Factories;

public class BlueskyAgentFactory(
    IOptions<BlueskyOptions> options,
    ILogger<BlueskyAgentFactory> logger,
    ILoggerFactory loggerFactory
) : IBlueskyAgentFactory
{
    private BlueskyOptions _options = options.Value;

    public async Task<BlueskyAgent> Create()
    {
        var agent = new BlueskyAgent(new BlueskyAgentOptions(loggerFactory));
        var result = await agent.Login(_options.Identifier, _options.Password);
        if (!result.Succeeded)
        {
            logger.LogError(
                "Bluesky login failed. Status-code: {statusCode}, error-detail-error: {errorDetailError}, error-detail-message: {errorDetailMessage}.",
                result.StatusCode, result.AtErrorDetail?.Error, result.AtErrorDetail?.Message);
        }

        return agent;
    }
}