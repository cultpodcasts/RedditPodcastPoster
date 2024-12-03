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
            .AddScoped<INonPodcastServiceCategoriser, NonPodcastServiceCategoriser>()
            .AddScoped<IPodcastServicesEpisodeEnricher, PodcastServicesEpisodeEnricher>()
            .AddScoped<IStreamingServiceMetaDataHandler, StreamingServiceMetaDataHandler>();
    }

    public static IServiceCollection AddRemoteClient(this IServiceCollection services)
    {
        services.AddHttpClient<IRemoteClient, RemoteClient>();
        return services.AddScoped<IRemoteClient, RemoteClient>();
    }
}