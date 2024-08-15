using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Reddit;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.Reddit;

public class AdminRedditClientFactory(IOptions<AdminRedditSettings> redditSettings) : IAdminRedditClientFactory
{
    private readonly AdminRedditSettings _redditSettings = redditSettings.Value;

    public IAdminRedditClient Create()
    {
        return new AdminRedditClient(new RedditClient(
            _redditSettings.AppId,
            appSecret: _redditSettings.AppSecret,
            refreshToken: _redditSettings.RefreshToken));
    }

    public static IServiceCollection AddAdminRedditClient(IServiceCollection services)
    {
        services.BindConfiguration<AdminRedditSettings>("redditAdmin");

        return services
            .AddScoped<IAdminRedditClientFactory, AdminRedditClientFactory>()
            .AddScoped(s => s.GetService<IAdminRedditClientFactory>()!.Create());
    }
}