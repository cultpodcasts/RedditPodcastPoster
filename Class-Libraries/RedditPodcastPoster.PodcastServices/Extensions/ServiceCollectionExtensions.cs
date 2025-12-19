using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddPodcastServices()
        {
            return services
                .AddScoped<IFlushable, CacheFlusher>()
                .AddScoped<IPodcastsUpdater, PodcastsUpdater>()
                .AddScoped<IPodcastUpdater, PodcastUpdater>()
                .AddScoped<INonPodcastServiceCategoriser, NonPodcastServiceCategoriser>()
                .AddScoped<IPodcastServicesEpisodeEnricher, PodcastServicesEpisodeEnricher>()
                .AddScoped<IStreamingServiceMetaDataHandler, StreamingServiceMetaDataHandler>()
                .AddScoped<IImageUpdater, ImageUpdater>()
                .AddScoped<IIndexablePodcastIdProvider, IndexablePodcastIdProvider>();
        }

        public IServiceCollection AddRemoteClient()
        {
            services.AddHttpClient<IRemoteClient, RemoteClient>();
            return services.AddScoped<IRemoteClient, RemoteClient>();
        }
    }
}