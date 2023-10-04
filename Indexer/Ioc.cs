using System.Net.Http.Headers;
using System.Text.Json;
using Indexer.Data;
using Indexer.Publishing;
using Indexer.Tweets;
using iTunesSearch.Library;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.EliminationTerms;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Common.KnownTerms;
using RedditPodcastPoster.Common.Matching;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices;
using RedditPodcastPoster.Common.PodcastServices.Apple;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using RedditPodcastPoster.Common.PodcastServices.YouTube;
using RedditPodcastPoster.Common.Reddit;
using RedditPodcastPoster.Common.Text;

namespace Indexer;

public static class Ioc
{
    public static void ConfigureServices(
        HostBuilderContext hostBuilderContext, 
        IServiceCollection serviceCollection)
    {
        serviceCollection
            .AddLogging()
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights()

            // Common
            .AddScoped<IDataRepository, CosmosDbRepository>()
            .AddScoped<ICosmosDbKeySelector, CosmosDbKeySelector>()
            .AddScoped<IEpisodeMatcher, EpisodeMatcher>()
            .AddScoped<IPodcastRepository, PodcastRepository>()
            .AddSingleton(new JsonSerializerOptions
            {
                WriteIndented = true
            })

            // Indexer
            .AddScoped<IPodcastsUpdater, PodcastsUpdater>()
            .AddScoped<IPodcastUpdater, PodcastUpdater>()
            .AddScoped<IEpisodeProvider, EpisodeProvider>()
            .AddScoped<ISpotifyEpisodeProvider, SpotifyEpisodeProvider>()
            .AddScoped<IYouTubeEpisodeProvider, YouTubeEpisodeProvider>()
            .AddScoped<IPodcastServicesEpisodeEnricher, PodcastServicesEpisodeEnricher>()
            .AddScoped(s => new iTunesSearchManager())
            .AddScoped<IApplePodcastResolver, ApplePodcastResolver>()
            .AddScoped<IAppleEpisodeResolver, AppleEpisodeResolver>()
            .AddScoped<IApplePodcastEnricher, ApplePodcastEnricher>()
            .AddScoped<IApplePodcastService, ApplePodcastService>()
            .AddScoped<IAppleEpisodeProvider, AppleEpisodeProvider>()
            .AddScoped<ICachedApplePodcastService, CachedApplePodcastService>()
            .AddScoped<ICachedSpotifyClient, CachedSpotifyClient>()
            .AddScoped<IRemoteClient, RemoteClient>()
            .AddScoped<IYouTubeItemResolver, YouTubeItemResolver>()
            .AddScoped<IYouTubeSearchService, YouTubeSearchService>()
            .AddScoped<IYouTubeSearcher, YouTubeSearcher>()
            .AddScoped<ISpotifyPodcastEnricher, SpotifyPodcastEnricher>()
            .AddScoped<ISpotifyIdResolver, SpotifyIdResolver>()
            .AddScoped<ISpotifyItemResolver, SpotifyItemResolver>()
            .AddScoped<ISpotifySearcher, SpotifySearcher>()
            .AddScoped<IPodcastFilter, PodcastFilter>()
            .AddSingleton<IAppleBearerTokenProvider, AppleBearerTokenProvider>()
            .AddSingleton<IJsonSerializerOptionsProvider, JsonSerializerOptionsProvider>()
            .AddScoped<IEliminationTermsRepository, EliminationTermsRepository>()
            .AddScoped<IEliminationTermsProviderFactory, EliminationTermsProviderFactory>()
            .AddSingleton(s => s.GetService<IEliminationTermsProviderFactory>().Create().GetAwaiter().GetResult())
            .AddScoped<IKnownTermsProviderFactory, KnownTermsProviderFactory>()
            .AddScoped<IKnownTermsRepository, KnownTermsRepository>()
            .AddSingleton(s => s.GetService<IKnownTermsProviderFactory>().Create().GetAwaiter().GetResult())

            // Poster 
            .AddScoped<IEpisodeProcessor, EpisodeProcessor>()
            .AddScoped<IEpisodeResolver, EpisodeResolver>()
            .AddSingleton<ITextSanitiser, TextSanitiser>()
            .AddScoped<IRedditPostTitleFactory, RedditPostTitleFactory>()
            .AddScoped<IEpisodePostManager, EpisodePostManager>()
            .AddScoped<IResolvedPodcastEpisodePoster, ResolvedPodcastEpisodePoster>()
            .AddScoped<IRedditLinkPoster, RedditLinkPoster>()
            .AddScoped<IRedditEpisodeCommentFactory, RedditEpisodeCommentFactory>()
            .AddScoped<IRedditBundleCommentFactory, RedditBundleCommentFactory>()

            // Content Publisher
            .AddScoped<IQueryExecutor, QueryExecutor>()
            .AddScoped<ITextSanitiser, TextSanitiser>()
            .AddScoped<IContentPublisher, ContentPublisher>()
            .AddScoped<IAmazonS3ClientFactory, AmazonS3ClientFactory>()
            .AddScoped(s => s.GetService<IAmazonS3ClientFactory>().Create())

            // Tweet
            .AddScoped<ITwitterClient, TwitterClient>()
            .AddScoped<ITweeter, Tweeter>();


        // Indexer
        serviceCollection.AddHttpClient<IAppleBearerTokenProvider, AppleBearerTokenProvider>();
        serviceCollection.AddHttpClient<IRemoteClient, RemoteClient>();
        serviceCollection.AddHttpClient();
        serviceCollection.AddHttpClient<IApplePodcastService, ApplePodcastService>((services, httpClient) =>
        {
            var appleBearerTokenProvider = services.GetService<IAppleBearerTokenProvider>();
            httpClient.BaseAddress = new Uri("https://amp-api.podcasts.apple.com/");
            httpClient.DefaultRequestHeaders.Authorization =
                appleBearerTokenProvider!.GetHeader().GetAwaiter().GetResult();
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
            httpClient.DefaultRequestHeaders.Referrer = new Uri("https://podcasts.apple.com/");
            httpClient.DefaultRequestHeaders.Add("Origin", "https://podcasts.apple.com");
            httpClient.DefaultRequestHeaders.UserAgent.Clear();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/118.0");
        });

        // Common
        CosmosDbClientFactory.AddCosmosClient(serviceCollection);

        // Indexer
        SpotifyClientFactory.AddSpotifyClient(serviceCollection);
        YouTubeServiceFactory.AddYouTubeService(serviceCollection);

        // Poster
        RedditClientFactory.AddRedditClient(serviceCollection);


        // Common
        serviceCollection
            .AddOptions<CosmosDbSettings>().Bind(hostBuilderContext.Configuration.GetSection("cosmosdb"));

        // Indexer
        serviceCollection
            .AddOptions<SpotifySettings>().Bind(hostBuilderContext.Configuration.GetSection("spotify"));
        serviceCollection
            .AddOptions<YouTubeSettings>().Bind(hostBuilderContext.Configuration.GetSection("youtube"));
        serviceCollection
            .AddOptions<IndexerOptions>().Bind(hostBuilderContext.Configuration.GetSection("indexer"));

        // Poster
        serviceCollection
            .AddOptions<PosterOptions>().Bind(hostBuilderContext.Configuration.GetSection("poster"));
        serviceCollection
            .AddOptions<RedditSettings>().Bind(hostBuilderContext.Configuration.GetSection("reddit"));
        serviceCollection
            .AddOptions<SubredditSettings>().Bind(hostBuilderContext.Configuration.GetSection("subreddit"));

        //Publisher
        serviceCollection
            .AddOptions<CloudFlareOptions>().Bind(hostBuilderContext.Configuration.GetSection("cloudflare"));

        //Tweet
        serviceCollection
            .AddOptions<TwitterOptions>().Bind(hostBuilderContext.Configuration.GetSection("twitter"));
    }
}