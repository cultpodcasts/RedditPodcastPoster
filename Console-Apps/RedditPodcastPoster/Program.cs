﻿using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using CommandLine;
using iTunesSearch.Library;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices;
using RedditPodcastPoster.Common.PodcastServices.Apple;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using RedditPodcastPoster.Common.PodcastServices.YouTube;
using RedditPodcastPoster.Matching;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube;
using RedditPodcastPoster.Reddit;
using RedditPodcastPoster.Text;
using RedditPodcastPoster.Text.EliminationTerms;
using RedditPodcastPoster.Text.KnownTerms;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", false)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddScoped<IPodcastsUpdater, PodcastsUpdater>()
    .AddScoped<IPodcastUpdater, PodcastUpdater>()
    .AddScoped<IEpisodeProvider, EpisodeProvider>()
    .AddScoped<IAppleEpisodeRetrievalHandler, AppleEpisodeRetrievalHandler>()
    .AddScoped<IYouTubeEpisodeRetrievalHandler, YouTubeEpisodeRetrievalHandler>()
    .AddScoped<ISpotifyEpisodeRetrievalHandler, SpotifyEpisodeRetrievalHandler>()
    .AddSingleton<IFoundEpisodeFilter, FoundEpisodeFilter>()
    .AddScoped<ISpotifyEpisodeProvider, SpotifyEpisodeProvider>()
    .AddScoped<IYouTubeEpisodeProvider, YouTubeEpisodeProvider>()
    .AddScoped<PodcastProcessor>()
    .AddScoped<IDataRepository, CosmosDbRepository>()
    .AddScoped<IPodcastRepository, PodcastRepository>()
    .AddScoped<IEpisodeMatcher, EpisodeMatcher>()
    .AddScoped<IPodcastServicesEpisodeEnricher, PodcastServicesEpisodeEnricher>()
    .AddScoped<IAppleEpisodeEnricher, AppleEpisodeEnricher>()
    .AddScoped<ISpotifyEpisodeEnricher, SpotifyEpisodeEnricher>()
    .AddScoped<IYouTubeEpisodeEnricher, YouTubeEpisodeEnricher>()
    .AddScoped(s => new iTunesSearchManager())
    .AddScoped<IApplePodcastResolver, ApplePodcastResolver>()
    .AddScoped<IAppleEpisodeResolver, AppleEpisodeResolver>()
    .AddScoped<IApplePodcastEnricher, ApplePodcastEnricher>()
    .AddScoped<IAppleEpisodeProvider, AppleEpisodeProvider>()
    .AddScoped<IApplePodcastService, ApplePodcastService>()
    .AddScoped<ICachedApplePodcastService, CachedApplePodcastService>()
    .AddScoped<IRemoteClient, RemoteClient>()
    .AddScoped<IEpisodeResolver, EpisodeResolver>()
    .AddSingleton<ITextSanitiser, TextSanitiser>()
    .AddScoped<IYouTubeItemResolver, YouTubeItemResolver>()
    .AddScoped<IYouTubePlaylistService, YouTubePlaylistService>()
    .AddScoped<IYouTubeVideoService, YouTubeVideoService>()
    .AddScoped<IYouTubeChannelVideoSnippetsService, YouTubeChannelVideoSnippetsService>()
    .AddScoped<IYouTubeChannelService, YouTubeChannelService>()
    .AddSingleton<IYouTubeIdExtractor, YouTubeIdExtractor>()
    .AddScoped<IYouTubeSearcher, YouTubeSearcher>()
    .AddScoped<ISpotifyPodcastEnricher, SpotifyPodcastEnricher>()
    .AddScoped<ISpotifyEpisodeResolver, SpotifyEpisodeResolver>()
    .AddScoped<ISpotifyPodcastResolver, SpotifyPodcastResolver>()
    .AddScoped<ISpotifyQueryPaginator, SpotifyQueryPaginator>()
    .AddScoped<ISpotifyClientWrapper, SpotifyClientWrapper>()
    .AddScoped<ISpotifySearcher, SpotifySearcher>()
    .AddScoped<IEpisodeProcessor, EpisodeProcessor>()
    .AddScoped<IRedditPostTitleFactory, RedditPostTitleFactory>()
    .AddScoped<IEpisodePostManager, EpisodePostManager>()
    .AddScoped<IPodcastEpisodePoster, PodcastEpisodePoster>()
    .AddScoped<IRedditLinkPoster, RedditLinkPoster>()
    .AddScoped<IRedditEpisodeCommentFactory, RedditEpisodeCommentFactory>()
    .AddScoped<IRedditBundleCommentFactory, RedditBundleCommentFactory>()
    .AddScoped<IPodcastFilter, PodcastFilter>()
    .AddSingleton<IJsonSerializerOptionsProvider, JsonSerializerOptionsProvider>()
    .AddScoped<IEliminationTermsRepository, EliminationTermsRepository>()
    .AddScoped<IEliminationTermsProviderFactory, EliminationTermsProviderFactory>()
    .AddSingleton(s => s.GetService<IEliminationTermsProviderFactory>()!.Create().GetAwaiter().GetResult())
    .AddScoped<IKnownTermsProviderFactory, KnownTermsProviderFactory>()
    .AddScoped<IKnownTermsRepository, KnownTermsRepository>()
    .AddScoped<IFlushable, CacheFlusher>()
    .AddSingleton(s => s.GetService<IKnownTermsProviderFactory>()!.Create().GetAwaiter().GetResult())
    .AddSingleton<IAppleBearerTokenProvider, AppleBearerTokenProvider>()
    .AddSingleton(new JsonSerializerOptions
    {
        WriteIndented = true
    });

builder.Services.AddHttpClient<IApplePodcastService, ApplePodcastService>((services, httpClient) =>
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

builder.Services.AddHttpClient<IRemoteClient, RemoteClient>();

SpotifyClientFactory.AddSpotifyClient(builder.Services);
RedditClientFactory.AddRedditClient(builder.Services);
YouTubeServiceFactory.AddYouTubeService(builder.Services);
CosmosDbClientFactory.AddCosmosClient(builder.Services);

builder.Services
    .AddOptions<SpotifySettings>().Bind(builder.Configuration.GetSection("spotify"));
builder.Services
    .AddOptions<RedditSettings>().Bind(builder.Configuration.GetSection("reddit"));
builder.Services
    .AddOptions<SubredditSettings>().Bind(builder.Configuration.GetSection("subreddit"));
builder.Services
    .AddOptions<YouTubeSettings>().Bind(builder.Configuration.GetSection("youtube"));
builder.Services
    .AddOptions<CosmosDbSettings>().Bind(builder.Configuration.GetSection("cosmosdb"));
builder.Services
    .AddOptions<PostingCriteria>().Bind(builder.Configuration.GetSection("postingCriteria"));


using var host = builder.Build();

var logger = host.Services.GetService<ILogger<Program>>();

return await Parser.Default.ParseArguments<ProcessRequest>(args)
    .MapResult(async processRequest => await Run(processRequest), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(ProcessRequest request)
{
    logger!.LogInformation($"{nameof(Run)} initiated.");
    var podcastProcessor = host.Services.GetService<PodcastProcessor>()!;
    var result = await podcastProcessor.Process(request);
    logger!.LogInformation($"{nameof(Run)} Operation results: '{result}'.");
    logger!.LogInformation($"{nameof(Run)} complete.");
    return result.ToResultCode();
}