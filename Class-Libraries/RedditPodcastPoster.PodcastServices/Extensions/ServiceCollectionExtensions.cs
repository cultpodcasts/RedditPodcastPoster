// pragma: allowlist secret
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.AmazonPrime.Extensions;
using RedditPodcastPoster.BBC.Extensions;
using RedditPodcastPoster.Channel4.Extensions;
using RedditPodcastPoster.DiscoveryPlus.Extensions; // pragma: allowlist secret
using RedditPodcastPoster.DisneyPlus.Extensions;
using RedditPodcastPoster.Fawesome.Extensions;
using RedditPodcastPoster.HboMax.Extensions;
using RedditPodcastPoster.InternetArchive.Extensions;
using RedditPodcastPoster.Itvx.Extensions;
using RedditPodcastPoster.Netflix.Extensions;
using RedditPodcastPoster.ParamountPlus.Extensions;
using RedditPodcastPoster.PlayRts.Extensions;
using RedditPodcastPoster.PlaySuisse.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Caches; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Abstractions.Clients; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Abstractions.Matching; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Abstractions.Heroes; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Categorisers; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Clients; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Enrichers; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Extensions; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Handlers; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Heroes; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Matching; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Merging; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Models; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Providers; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Updaters; // pragma: allowlist secret
using RedditPodcastPoster.TvnzPlus.Extensions;
using RedditPodcastPoster.BitChute.Extensions; // pragma: allowlist secret
using RedditPodcastPoster.Vimeo.Extensions;

namespace RedditPodcastPoster.PodcastServices.Extensions; // pragma: allowlist secret

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddPodcastServices() // pragma: allowlist secret
        {
            return services
                .AddSingleton<IEpisodeMatcher, EpisodeMatcher>()
                .AddSingleton<IEpisodeMerger, EpisodeMerger>()
                .AddScoped<IPodcastPassApiCache, PodcastPassApiCache>()
                .AddScoped<IPodcastsUpdater, PodcastsUpdater>() // pragma: allowlist secret
                .AddScoped<IPodcastUpdater, PodcastUpdater>()
                .AddScoped<IHeroEpisodePromoter, NullHeroEpisodePromoter>()
                .AddScoped<INonPodcastServiceCategoriser, NonPodcastServiceCategoriser>() // pragma: allowlist secret
                .AddScoped<INonPodcastServiceAdapterResolver, NonPodcastServiceAdapterResolver>() // pragma: allowlist secret
                .AddScoped<INonPodcastServiceAdapter, BbcNonPodcastServiceAdapter>() // pragma: allowlist secret
                .AddScoped<INonPodcastServiceAdapter, InternetArchiveNonPodcastServiceAdapter>() // pragma: allowlist secret
                .AddScoped<IPodcastServicesEpisodeEnricher, PodcastServicesEpisodeEnricher>() // pragma: allowlist secret
                .AddScoped<IStreamingServiceMetaDataHandler, StreamingServiceMetaDataHandler>()
                .AddScoped<IImageUpdater, ImageUpdater>()
                .AddScoped<IIndexablePodcastIdProvider, IndexablePodcastIdProvider>();
        }

        public IServiceCollection AddNonPodcastScrapers() // pragma: allowlist secret
        {
            return services
                .AddBBCServices()
                .AddInternetArchiveServices()
                .AddVimeoServices()
                .AddBitChuteServices() // pragma: allowlist secret
                .AddNetflixServices()
                .AddAmazonPrimeServices()
                .AddItvxServices()
                .AddChannel4Services()
                .AddFawesomeServices()
                .AddDisneyPlusServices()
                .AddDiscoveryPlusServices() // pragma: allowlist secret
                .AddParamountPlusServices()
                .AddHboMaxServices()
                .AddPlaySuisseServices()
                .AddPlayRtsServices()
                .AddTvnzPlusServices();
        }

        public IServiceCollection AddRemoteClient()
        {
            services.AddHttpClient<IRemoteClient, RemoteClient>();
            return services.AddScoped<IRemoteClient, RemoteClient>();
        }
    }
}
