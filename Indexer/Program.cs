using System.Net.Http.Headers;
using System.Text.Json;
using Azure;
using Indexer;
using iTunesSearch.Library;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.EliminationTerms;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices;
using RedditPodcastPoster.Common.PodcastServices.Apple;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using RedditPodcastPoster.Common.PodcastServices.YouTube;
using RedditPodcastPoster.Common.Reddit;
using RedditPodcastPoster.Common.Text;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(
        builder => { builder.Services.ConfigureFunctionsApplicationInsights(); })
    .ConfigureServices((context, services) =>
    {
        services.AddLogging()
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights()
            .AddScoped<IPodcastsUpdater, PodcastsUpdater>()
            .AddScoped<IPodcastUpdater, PodcastUpdater>()
            .AddScoped<IEpisodeProvider, EpisodeProvider>()
            .AddScoped<ISpotifyEpisodeProvider, SpotifyEpisodeProvider>()
            .AddScoped<IYouTubeEpisodeProvider, YouTubeEpisodeProvider>()
            .AddScoped<IDataRepository, CosmosDbRepository>()
            .AddScoped<ICosmosDbKeySelector, CosmosDbKeySelector>()
            .AddScoped<IPodcastRepository, PodcastRepository>()
            .AddScoped<IPodcastServicesEpisodeEnricher, PodcastServicesEpisodeEnricher>()
            .AddScoped(s => new iTunesSearchManager())
            .AddScoped<IApplePodcastResolver, ApplePodcastResolver>()
            .AddScoped<IAppleEpisodeResolver, AppleEpisodeResolver>()
            .AddScoped<IApplePodcastEnricher, ApplePodcastEnricher>()
            .AddScoped<IApplePodcastService, ApplePodcastService>()
            .AddScoped<ICachedApplePodcastService, CachedApplePodcastService>()
            .AddScoped<IRemoteClient, RemoteClient>()
            .AddScoped<IYouTubeItemResolver, YouTubeItemResolver>()
            .AddScoped<IYouTubeSearchService, YouTubeSearchService>()
            .AddScoped<IYouTubeSearcher, YouTubeSearcher>()
            .AddScoped<ISpotifyPodcastEnricher, SpotifyPodcastEnricher>()
            .AddScoped<ISpotifyIdResolver, SpotifyIdResolver>()
            .AddScoped<ISpotifyItemResolver, SpotifyItemResolver>()
            .AddScoped<ISpotifySearcher, SpotifySearcher>()
            .AddScoped<IEliminationTermsRepository, EliminationTermsRepository>()
            .AddScoped<IPodcastFilter, PodcastFilter>()
            .AddSingleton<IAppleBearerTokenProvider, AppleBearerTokenProvider>()
            .AddSingleton(new JsonSerializerOptions
            {
                WriteIndented = true
            });

        services.AddHttpClient<IAppleBearerTokenProvider, AppleBearerTokenProvider>();
        services.AddHttpClient<IRemoteClient, RemoteClient>();
        services.AddHttpClient();

        services.AddHttpClient<IApplePodcastService, ApplePodcastService>((services, httpClient) =>
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


        SpotifyClientFactory.AddSpotifyClient(services);
        YouTubeServiceFactory.AddYouTubeService(services);
        CosmosDbClientFactory.AddCosmosClient(services);

        services
            .AddOptions<SpotifySettings>().Bind(context.Configuration.GetSection("spotify"));
        services
            .AddOptions<YouTubeSettings>().Bind(context.Configuration.GetSection("youtube"));
        services
            .AddOptions<CosmosDbSettings>().Bind(context.Configuration.GetSection("cosmosdb"));
        services
            .AddOptions<IndexerOptions>().Bind(context.Configuration.GetSection("indexer"));
    })
    .ConfigureLogging(logging => { logging.AllowAzureFunctionApplicationInsightsTraceLogging(); })
    .Build();

host.Run();