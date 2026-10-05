using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Tmdb.Clients;
using RedditPodcastPoster.Tmdb.Configuration;

namespace RedditPodcastPoster.Tmdb.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTmdbClient(this IServiceCollection services)
    {
        services.AddOptions<TmdbOptions>().BindConfiguration("tmdb");
        services.AddHttpClient<ITmdbClient, TmdbClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.themoviedb.org/3/");
        });
        return services;
    }

    public static IServiceCollection AddTmdbClient(this IServiceCollection services, Action<TmdbOptions> configure)
    {
        services.AddTmdbClient();
        services.Configure(configure);
        return services;
    }
}
