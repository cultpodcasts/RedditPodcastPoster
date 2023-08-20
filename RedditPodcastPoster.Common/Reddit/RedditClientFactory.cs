using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reddit;

namespace RedditPodcastPoster.Common.Reddit;

public class RedditClientFactory  : IRedditClientFactory
{
    private readonly RedditSettings _redditSettings;
    private readonly ILogger<RedditClientFactory> _logger;

    public RedditClientFactory(IOptions<RedditSettings> redditSettings, ILogger<RedditClientFactory> logger)
    {
        _redditSettings = redditSettings.Value;
        _logger = logger;
    }

    public RedditClient Create()
    {
        return new RedditClient(
            appId: _redditSettings.AppId, 
            appSecret: _redditSettings.AppSecret,
            refreshToken: _redditSettings.RefreshToken);
    }

    public static IServiceCollection AddRedditClient(IServiceCollection services)
    {
        return services
            .AddScoped<IRedditClientFactory, RedditClientFactory>()
            .AddScoped(s => s.GetService<IRedditClientFactory>().Create());
    }
    
}