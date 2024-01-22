using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reddit;

namespace RedditPodcastPoster.Reddit;

public class RedditClientFactory(IOptions<RedditSettings> redditSettings, ILogger<RedditClientFactory> logger)
    : IRedditClientFactory
{
    private readonly RedditSettings _redditSettings = redditSettings.Value;

    public RedditClient Create()
    {
        return new RedditClient(
            _redditSettings.AppId,
            appSecret: _redditSettings.AppSecret,
            refreshToken: _redditSettings.RefreshToken);
    }

    public static IServiceCollection AddRedditClient(IServiceCollection services)
    {
        return services
            .AddScoped<IRedditClientFactory, RedditClientFactory>()
            .AddScoped(s => s.GetService<IRedditClientFactory>()!.Create());
    }
}