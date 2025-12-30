using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Reddit;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.Reddit;

public class AdminRedditClientFactory(
    IOptions<AdminRedditSettings> adminRedditSettings, 
    IOptions<RedditSettings> redditSettings
    ) : IAdminRedditClientFactory {

    private readonly AdminRedditSettings _adminRedditSettings = adminRedditSettings.Value;
    private readonly RedditSettings _redditSettings = redditSettings.Value;

    public IAdminRedditClient Create()
    {
        return new AdminRedditClient(new RedditClient(
            _adminRedditSettings.AppId,
            appSecret: _adminRedditSettings.AppSecret,
            refreshToken: _adminRedditSettings.RefreshToken,
            userAgent: _redditSettings.UserAgent));
    }

    public static IServiceCollection AddAdminRedditClient(IServiceCollection services)
    {
        return services
            .AddScoped<IAdminRedditClientFactory, AdminRedditClientFactory>()
            .AddScoped(s => s.GetService<IAdminRedditClientFactory>()!.Create())
            .BindConfiguration<AdminRedditSettings>("redditAdmin");
    }
}