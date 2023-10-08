using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using CommandLine;
using iTunesSearch.Library;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Matching;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices.Apple;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using RedditPodcastPoster.Common.PodcastServices.YouTube;
using RedditPodcastPoster.Common.UrlCategorisation;
using RedditPodcastPoster.Common.UrlSubmission;
using SubmitUrl;

var builder = Host.CreateApplicationBuilder(args);


builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddScoped<IDataRepository, CosmosDbRepository>()
    .AddScoped<IPartitionKeySelector, PartitionKeySelector>()
    .AddScoped<UrlSubmitter>()
    .AddScoped<IPodcastRepository, PodcastRepository>()
    .AddScoped<IEpisodeMatcher, EpisodeMatcher>()
    .AddScoped<IUrlCategoriser, UrlCategoriser>()
    .AddScoped<IAppleUrlCategoriser, AppleUrlCategoriser>()
    .AddScoped<ISpotifyUrlCategoriser, SpotifyUrlCategoriser>()
    .AddScoped<IYouTubeUrlCategoriser, YouTubeUrlCategoriser>()
    .AddScoped<ISpotifyItemResolver, SpotifyItemResolver>()
    .AddScoped<ISpotifySearcher, SpotifySearcher>()
    .AddScoped<IAppleEpisodeResolver, AppleEpisodeResolver>()
    .AddScoped<IRemoteClient, RemoteClient>()
    .AddScoped<IApplePodcastResolver, ApplePodcastResolver>()
    .AddScoped(s => new iTunesSearchManager())
    .AddScoped<IApplePodcastService, ApplePodcastService>()
    .AddScoped<ICachedApplePodcastService, CachedApplePodcastService>()
    .AddScoped<IYouTubeSearchService, YouTubeSearchService>()
    .AddSingleton<IAppleBearerTokenProvider, AppleBearerTokenProvider>()
    .AddScoped<IUrlSubmitter, UrlSubmitter>()
    .AddScoped<SubmitUrlProcessor>()
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
return await Parser.Default.ParseArguments<SubmitUrlRequest>(args)
    .MapResult(async submitUrlRequest => await Run(submitUrlRequest), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(SubmitUrlRequest request)
{
    var urlSubmitter = host.Services.GetService<SubmitUrlProcessor>()!;
    await urlSubmitter.Process(request);
    return 0;
}