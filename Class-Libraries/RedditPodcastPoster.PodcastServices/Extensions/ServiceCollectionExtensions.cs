using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.PodcastServices.Thumbnails;
using RedditPodcastPoster.AmazonPrime.Extensions;
using RedditPodcastPoster.AppleTvPlus.Extensions;
using RedditPodcastPoster.Ard.Extensions;
using RedditPodcastPoster.Arte.Extensions;
using RedditPodcastPoster.BBC.Extensions;
using RedditPodcastPoster.CanalPlus.Extensions;
using RedditPodcastPoster.Channel4.Extensions;
using RedditPodcastPoster.DiscoveryPlus.Extensions;
using RedditPodcastPoster.DisneyPlus.Extensions;
using RedditPodcastPoster.Fawesome.Extensions;
using RedditPodcastPoster.FranceTv.Extensions;
using RedditPodcastPoster.HboMax.Extensions;
using RedditPodcastPoster.InternetArchive.Extensions;
using RedditPodcastPoster.Itvx.Extensions;
using RedditPodcastPoster.Netflix.Extensions;
using RedditPodcastPoster.ParamountPlus.Extensions;
using RedditPodcastPoster.Peacock.Extensions;
using RedditPodcastPoster.PlayRts.Extensions;
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
using RedditPodcastPoster.AbcIview.Extensions;
using RedditPodcastPoster.CbcGem.Extensions;
using RedditPodcastPoster.TvnzPlus.Extensions;
using RedditPodcastPoster.BcVideo.Extensions;
using RedditPodcastPoster.Tubi.Extensions;
using RedditPodcastPoster.Vimeo.Extensions;
using RedditPodcastPoster.Zdf.Extensions;
using RedditPodcastPoster.UrlSubmission.Extensions;

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
                .AddScoped<ICatalogueParentsUpdater, CatalogueParentsUpdater>()
                .AddScoped<IHeroEpisodePromoter, NullHeroEpisodePromoter>()
                .AddScoped<INonPodcastServiceCategoriser, NonPodcastServiceCategoriser>()
                .AddScoped<INonPodcastServiceAdapterResolver, NonPodcastServiceAdapterResolver>()
                .AddScoped<INonPodcastServiceAdapter, BbcNonPodcastServiceAdapter>()
                .AddScoped<INonPodcastServiceAdapter, InternetArchiveNonPodcastServiceAdapter>()
                .AddScoped<IPodcastServicesEpisodeEnricher, PodcastServicesEpisodeEnricher>()
                .AddScoped<IStreamingServiceMetaDataHandler, StreamingServiceMetaDataHandler>()
                .AddScoped<IImageUpdater, ImageUpdater>()
                .AddScoped<IEpisodeServiceMaterialiser, EpisodeServiceMaterialiser>()
                .AddScoped<IIndexablePodcastIdProvider, IndexablePodcastIdProvider>()
                .BindConfiguration<PodcastUpdateBudgetOptions>(PodcastUpdateBudgetOptions.SectionName)
                .BindConfiguration<PodcastIndexingStageLogOptions>(PodcastIndexingStageLogOptions.SectionName)
                .AddSingleton<PodcastUpdateBudgetCutTracker>()
                .AddYouTubeThumbnailRecheck()
                .AddUrlMembershipLookup();
        }

        /// <summary>
        /// Delayed re-check of recent YouTube images on withdrawable high-resolution tiers.
        /// Options bind from <see cref="YouTubeThumbnailRecheckOptions.SectionName"/> and are validated on start.
        /// </summary>
        public IServiceCollection AddYouTubeThumbnailRecheck()
        {
            services.TryAddSingleton(TimeProvider.System);
            services
                .AddScoped<IRecentYouTubeThumbnailRechecker, RecentYouTubeThumbnailRechecker>()
                .BindConfiguration<YouTubeThumbnailRecheckOptions>(YouTubeThumbnailRecheckOptions.SectionName)
                .AddSingleton<IValidateOptions<YouTubeThumbnailRecheckOptions>, YouTubeThumbnailRecheckOptionsValidator>()
                .AddOptions<YouTubeThumbnailRecheckOptions>().ValidateOnStart();
            return services;
        }

        public IServiceCollection AddNonPodcastScrapers()
        {
            return services
                .AddBBCServices()
                .AddInternetArchiveServices()
                .AddVimeoServices()
                .AddBcVideoServices()
                .AddTubiServices()
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
                .AddPlayRtsServices()
                .AddTvnzPlusServices()
                .AddFranceTvServices()
                .AddArteServices()
                .AddPeacockServices()
                .AddAppleTvPlusServices()
                .AddZdfServices()
                .AddArdServices()
                .AddCanalPlusServices()
                .AddAbcIviewServices()
                .AddCbcGemServices();
        }

        public IServiceCollection AddRemoteClient()
        {
            services.AddHttpClient<IRemoteClient, RemoteClient>();
            return services.AddScoped<IRemoteClient, RemoteClient>();
        }
    }
}
