using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.TheTvdb.Clients;
using RedditPodcastPoster.TheTvdb.Configuration;

namespace RedditPodcastPoster.TheTvdb.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTheTvdbClient(this IServiceCollection services)
    {
        services.AddOptions<TheTvdbOptions>().BindConfiguration("thetvdb");
        services.AddHttpClient<ITheTvdbClient, TheTvdbClient>(client =>
        {
            client.BaseAddress = new Uri("https://api4.thetvdb.com/v4/");
        });
        return services;
    }

    public static IServiceCollection AddTheTvdbClient(
        this IServiceCollection services,
        Action<TheTvdbOptions> configure)
    {
        services.AddTheTvdbClient();
        services.Configure(configure);
        return services;
    }
}
