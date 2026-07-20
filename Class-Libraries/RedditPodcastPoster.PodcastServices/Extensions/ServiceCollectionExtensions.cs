using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Clients;

namespace RedditPodcastPoster.PodcastServices.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddPodcastServices()
        {
            return services
                .AddSingleton<IEpisodeMatcher, EpisodeMatcher>()
                .AddSingleton<IEpisodeMerger, EpisodeMerger>()
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
