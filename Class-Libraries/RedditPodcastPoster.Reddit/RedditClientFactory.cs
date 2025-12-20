using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reddit;

namespace RedditPodcastPoster.Reddit;

public class RedditClientFactory(
    IOptions<RedditSettings> redditSettings,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<RedditClientFactory> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : IRedditClientFactory
{
    private readonly RedditSettings _redditSettings = redditSettings.Value;

    public RedditClient Create()
    {
        return new RedditClient(
            _redditSettings.AppId,
            appSecret: _redditSettings.AppSecret,
            refreshToken: _redditSettings.RefreshToken,
            userAgent: _redditSettings.UserAgent);
    }

    public static IServiceCollection AddRedditClient(IServiceCollection services)
    {
        return services
            .AddScoped<IRedditClientFactory, RedditClientFactory>()
            .AddScoped(s => s.GetService<IRedditClientFactory>()!.Create());
    }
}