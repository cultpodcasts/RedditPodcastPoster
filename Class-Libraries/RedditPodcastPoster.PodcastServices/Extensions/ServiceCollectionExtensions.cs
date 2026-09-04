using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.AmazonPrime.Extensions;
using RedditPodcastPoster.BBC.Extensions;
using RedditPodcastPoster.Channel4.Extensions;
using RedditPodcastPoster.DiscoveryPlus.Extensions;
using RedditPodcastPoster.DisneyPlus.Extensions;
using RedditPodcastPoster.Fawesome.Extensions;
using RedditPodcastPoster.HboMax.Extensions;
using RedditPodcastPoster.InternetArchive.Extensions;
using RedditPodcastPoster.Itvx.Extensions;
using RedditPodcastPoster.Netflix.Extensions;
using RedditPodcastPoster.ParamountPlus.Extensions;
using RedditPodcastPoster.PlaySuisse.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Caches;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Clients;
using RedditPodcastPoster.PodcastServices.Abstractions.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;
using RedditPodcastPoster.PodcastServices.Abstractions.Heroes;
using RedditPodcastPoster.PodcastServices.Categorisers;
using RedditPodcastPoster.PodcastServices.Clients;
using RedditPodcastPoster.PodcastServices.Enrichers;
using RedditPodcastPoster.PodcastServices.Extensions;
using RedditPodcastPoster.PodcastServices.Handlers;
using RedditPodcastPoster.PodcastServices.Heroes;
using RedditPodcastPoster.PodcastServices.Matching;
using RedditPodcastPoster.PodcastServices.Merging;
using RedditPodcastPoster.PodcastServices.Models;
using RedditPodcastPoster.PodcastServices.Providers;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.TvnzPlus.Extensions;
using RedditPodcastPoster.Vimeo.Extensions;

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
                .AddScoped<IPodcastPassApiCache, PodcastPassApiCache>()
                .AddScoped<IPodcastsUpdater, PodcastsUpdater>()
                .AddScoped<IPodcastUpdater, PodcastUpdater>()
                .AddScoped<IHeroEpisodePromoter, NullHeroEpisodePromoter>()
                .AddScoped<INonPodcastServiceCategoriser, NonPodcastServiceCategoriser>()
                .AddScoped<INonPodcastServiceAdapterResolver, NonPodcastServiceAdapterResolver>()
                .AddScoped<INonPodcastServiceAdapter, BbcNonPodcastServiceAdapter>()
                .AddScoped<INonPodcastServiceAdapter, InternetArchiveNonPodcastServiceAdapter>()
                .AddScoped<IPodcastServicesEpisodeEnricher, PodcastServicesEpisodeEnricher>()
                .AddScoped<IStreamingServiceMetaDataHandler, StreamingServiceMetaDataHandler>()
                .AddScoped<IImageUpdater, ImageUpdater>()
                .AddScoped<IIndexablePodcastIdProvider, IndexablePodcastIdProvider>();
        }

        public IServiceCollection AddNonPodcastScrapers()
        {
            return services
                .AddBBCServices()
                .AddInternetArchiveServices()
                .AddVimeoServices()
                .AddNetflixServices()
                .AddAmazonPrimeServices()
                .AddItvxServices()
                .AddChannel4Services()
                .AddFawesomeServices()
                .AddDisneyPlusServices()
                .AddDiscoveryPlusServices()
                .AddParamountPlusServices()
                .AddHboMaxServices()
                .AddPlaySuisseServices()
                .AddTvnzPlusServices();
        }

        public IServiceCollection AddRemoteClient()
        {
            services.AddHttpClient<IRemoteClient, RemoteClient>();
            return services.AddScoped<IRemoteClient, RemoteClient>();
        }
    }
}
