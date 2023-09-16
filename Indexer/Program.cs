using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using iTunesSearch.Library;
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
using System.Net.Http.Headers;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(builder =>
    {
        builder.Services.ConfigureFunctionsApplicationInsights();
    })
    .ConfigureServices((context, collection) =>
    {
        collection.AddLogging()
            .AddApplicationInsightsTelemetryWorkerService()
            .AddScoped<IPodcastsUpdater, PodcastsUpdater>()
            .AddScoped<IPodcastUpdater, PodcastUpdater>()
            .AddScoped<IEpisodeProvider, EpisodeProvider>()
            .AddScoped<ISpotifyEpisodeProvider, SpotifyEpisodeProvider>()
            .AddScoped<IYouTubeEpisodeProvider, YouTubeEpisodeProvider>()
            .AddScoped<IPodcastProcessor, PodcastProcessor>()
            .AddScoped<IFilenameSelector, FilenameSelector>()
            .AddScoped<IDataRepository, CosmosDbRepository>()
            .AddScoped<ICosmosDbKeySelector, CosmosDbKeySelector>()
            .AddScoped<IPodcastRepository, PodcastRepository>()
            .AddScoped<IPodcastServicesEpisodeEnricher, PodcastServicesEpisodeEnricher>()
            .AddScoped(s => new iTunesSearchManager())
            .AddScoped<IApplePodcastResolver, ApplePodcastResolver>()
            .AddScoped<IAppleEpisodeResolver, AppleEpisodeResolver>()
            .AddScoped<IApplePodcastEnricher, ApplePodcastEnricher>()
            //    .AddScoped<IApplePodcastService, RecentApplePodcastService>()
            .AddScoped<IApplePodcastService, ApplePodcastService>()
            .AddScoped<ICachedApplePodcastService, CachedApplePodcastService>()
            .AddScoped<IRemoteClient, RemoteClient>()
            .AddScoped<IEpisodeResolver, EpisodeResolver>()
            .AddSingleton<ITextSanitiser, TextSanitiser>()
            .AddScoped<IYouTubeItemResolver, YouTubeItemResolver>()
            .AddScoped<IYouTubeSearchService, YouTubeSearchService>()
            .AddScoped<IYouTubeSearcher, YouTubeSearcher>()
            .AddScoped<ISpotifyPodcastEnricher, SpotifyPodcastEnricher>()
            .AddScoped<ISpotifyIdResolver, SpotifyIdResolver>()
            .AddScoped<ISpotifyItemResolver, SpotifyItemResolver>()
            .AddScoped<ISpotifySearcher, SpotifySearcher>()
            .AddScoped<IEpisodeProcessor, EpisodeProcessor>()
            .AddScoped<IRedditPostTitleFactory, RedditPostTitleFactory>()
            .AddScoped<IEpisodePostManager, EpisodePostManager>()
            .AddScoped<IResolvedPodcastEpisodeAdaptor, ResolvedPodcastEpisodeAdaptor>()
            .AddScoped<IResolvedPodcastEpisodePoster, ResolvedPodcastEpisodePoster>()
            .AddScoped<IRedditLinkPoster, RedditLinkPoster>()
            .AddScoped<IRedditEpisodeCommentFactory, RedditEpisodeCommentFactory>()
            .AddScoped<IRedditBundleCommentFactory, RedditBundleCommentFactory>()
            .AddScoped<IEliminationTermsRepository, EliminationTermsRepository>()
            .AddScoped<IPodcastFilter, PodcastFilter>()
            .AddSingleton<IAppleBearerTokenProvider, AppleBearerTokenProvider>()
            .AddSingleton(new JsonSerializerOptions
            {
                WriteIndented = true
            });

        collection.AddHttpClient<IRemoteClient, RemoteClient>();
        collection.AddHttpClient();

        collection.AddHttpClient<IApplePodcastService, ApplePodcastService>((services, httpClient) =>
        {
            var appleBearerTokenProvider = services.GetService<IAppleBearerTokenProvider>();
            httpClient.BaseAddress = new Uri("https://amp-api.podcasts.apple.com/");
            httpClient.DefaultRequestHeaders.Authorization = appleBearerTokenProvider!.GetHeader().GetAwaiter().GetResult();
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
            httpClient.DefaultRequestHeaders.Referrer = new Uri("https://podcasts.apple.com/");
            httpClient.DefaultRequestHeaders.Add("Origin", "https://podcasts.apple.com");
            httpClient.DefaultRequestHeaders.UserAgent.Clear();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/118.0");
        });


        SpotifyClientFactory.AddSpotifyClient(collection);
        RedditClientFactory.AddRedditClient(collection);
        YouTubeServiceFactory.AddYouTubeService(collection);
        CosmosDbClientFactory.AddCosmosClient(collection);

        collection
            .AddOptions<SpotifySettings>().Bind(context.Configuration.GetSection("spotify"));
        collection
            .AddOptions<RedditSettings>().Bind(context.Configuration.GetSection("reddit"));
        collection
            .AddOptions<SubredditSettings>().Bind(context.Configuration.GetSection("subreddit"));
        collection
            .AddOptions<YouTubeSettings>().Bind(context.Configuration.GetSection("youtube"));
        collection
            .AddOptions<CosmosDbSettings>().Bind(context.Configuration.GetSection("cosmosdb"));
        collection
            .AddOptions<PostingCriteria>().Bind(context.Configuration.GetSection("postingCriteria"));

    })
    .Build();

host.Run();