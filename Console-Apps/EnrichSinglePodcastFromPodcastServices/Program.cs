﻿using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using EnrichSinglePodcastFromPodcastServices;
using iTunesSearch.Library;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices;
using RedditPodcastPoster.Common.PodcastServices.Apple;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using RedditPodcastPoster.Common.PodcastServices.YouTube;
using RedditPodcastPoster.Matching;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube;
using RedditPodcastPoster.Text.EliminationTerms;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddSingleton(new JsonSerializerOptions
    {
        WriteIndented = true
    })
    .AddScoped<EnrichSinglePodcastFromPodcastServicesProcessor>()
    .AddScoped<IPodcastRepository, PodcastRepository>()
    .AddScoped<IDataRepository, CosmosDbRepository>()
    .AddSingleton<PodcastFactory>()
    .AddScoped<IApplePodcastEnricher, ApplePodcastEnricher>()
    .AddScoped<IApplePodcastResolver, ApplePodcastResolver>()
    .AddScoped(s => new iTunesSearchManager())
    .AddScoped<IRemoteClient, RemoteClient>()
    .AddScoped<IApplePodcastEnricher, ApplePodcastEnricher>()
    .AddScoped<IPodcastUpdater, PodcastUpdater>()
    .AddScoped<IEpisodeProvider, EpisodeProvider>()
    .AddScoped<IAppleEpisodeRetrievalHandler, AppleEpisodeRetrievalHandler>()
    .AddScoped<IYouTubeEpisodeRetrievalHandler, YouTubeEpisodeRetrievalHandler>()
    .AddScoped<ISpotifyEpisodeRetrievalHandler, SpotifyEpisodeRetrievalHandler>()
    .AddSingleton<IFoundEpisodeFilter, FoundEpisodeFilter>()
    .AddScoped<ISpotifyEpisodeProvider, SpotifyEpisodeProvider>()
    .AddScoped<IAppleEpisodeProvider, AppleEpisodeProvider>()
    .AddScoped<ISpotifyEpisodeResolver, SpotifyEpisodeResolver>()
    .AddScoped<ISpotifyPodcastResolver, SpotifyPodcastResolver>()
    .AddScoped<ISpotifyQueryPaginator, SpotifyQueryPaginator>()
    .AddScoped<ISpotifyClientWrapper, SpotifyClientWrapper>()
    .AddScoped<ISpotifySearcher, SpotifySearcher>()
    .AddScoped<IAppleEpisodeProvider, AppleEpisodeProvider>()
    .AddScoped<IYouTubeEpisodeProvider, YouTubeEpisodeProvider>()
    .AddScoped<IYouTubePlaylistService, YouTubePlaylistService>()
    .AddScoped<IYouTubeVideoService, YouTubeVideoService>()
    .AddScoped<IYouTubeChannelVideoSnippetsService, YouTubeChannelVideoSnippetsService>()
    .AddScoped<IYouTubeItemResolver, YouTubeItemResolver>()
    .AddScoped<IYouTubeSearcher, YouTubeSearcher>()
    .AddScoped<IPodcastServicesEpisodeEnricher, PodcastServicesEpisodeEnricher>()
    .AddScoped<IAppleEpisodeEnricher, AppleEpisodeEnricher>()
    .AddScoped<ISpotifyEpisodeEnricher, SpotifyEpisodeEnricher>()
    .AddScoped<IYouTubeEpisodeEnricher, YouTubeEpisodeEnricher>()
    .AddSingleton<IYouTubeIdExtractor, YouTubeIdExtractor>()
    .AddScoped<IAppleEpisodeResolver, AppleEpisodeResolver>()
    .AddScoped<IApplePodcastService, ApplePodcastService>()
    .AddScoped<ICachedApplePodcastService, CachedApplePodcastService>()
    .AddScoped<IPodcastFilter, PodcastFilter>()
    .AddSingleton<IJsonSerializerOptionsProvider, JsonSerializerOptionsProvider>()
    .AddScoped<IEliminationTermsRepository, EliminationTermsRepository>()
    .AddScoped<IEliminationTermsProviderFactory, EliminationTermsProviderFactory>()
    .AddSingleton(s => s.GetService<IEliminationTermsProviderFactory>()!.Create().GetAwaiter().GetResult())
    .AddScoped<IEpisodeMatcher, EpisodeMatcher>()
    .AddSingleton<IAppleBearerTokenProvider, AppleBearerTokenProvider>()
    .AddSingleton<IJsonSerializerOptionsProvider, JsonSerializerOptionsProvider>()
    .AddHttpClient<IApplePodcastService, ApplePodcastService>((services, httpClient) =>
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

CosmosDbClientFactory.AddCosmosClient(builder.Services);
SpotifyClientFactory.AddSpotifyClient(builder.Services);
YouTubeServiceFactory.AddYouTubeService(builder.Services);

builder.Services
    .AddOptions<CosmosDbSettings>().Bind(builder.Configuration.GetSection("cosmosdb"));
builder.Services
    .AddOptions<SpotifySettings>().Bind(builder.Configuration.GetSection("spotify"));
builder.Services
    .AddOptions<YouTubeSettings>().Bind(builder.Configuration.GetSection("youtube"));

using var host = builder.Build();


var podcastProcessor = host.Services.GetService<EnrichSinglePodcastFromPodcastServicesProcessor>()!;
if (Guid.TryParse(args[0], out var podcastId))
{
    await podcastProcessor.Run(podcastId);
}
else
{
    throw new ArgumentException($"Could not parse guid '{args[0]}'.");
}

return 0;