using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPodcastServices(this IServiceCollection services)
    {
        return services
            .AddScoped<IPodcastsUpdater, PodcastsUpdater>()
            .AddScoped<IPodcastUpdater, PodcastUpdater>()
            .AddScoped<IPodcastServicesEpisodeEnricher, PodcastServicesEpisodeEnricher>()
            .AddScoped<IPodcastEpisodeProvider, PodcastEpisodeProvider>();
    }

    public static IServiceCollection AddRemoteClient(this IServiceCollection services)
    {
        services.AddHttpClient<IRemoteClient, RemoteClient>();
        return services.AddScoped<IRemoteClient, RemoteClient>();
    }
}