using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using iTunesSearch.Library;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices.Apple;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using RedditPodcastPoster.Common.PodcastServices.YouTube;
using RedditPodcastPoster.Common.UrlCategorisation;
using SubmitUrl;

var builder = Host.CreateApplicationBuilder(args);

if (args.Length != 2)
    throw new InvalidOperationException("Requires 3 arguments - the url and apple.com bearer token");

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddScoped<IFilenameSelector, FilenameSelector>()
    .AddScoped<IFileRepositoryFactory, FileRepositoryFactory>()
    .AddScoped(services =>(IDataRepository) services.GetService<IFileRepositoryFactory>()!.Create("podcasts"))
    //.AddScoped<IDataRepository, CosmosDbRepository>()
    //.AddScoped<ICosmosDbKeySelector, CosmosDbKeySelector>()
    .AddSingleton(new JsonSerializerOptions
    {
        WriteIndented = true
    })
    .AddScoped<UrlSubmitter>()
    .AddScoped<IPodcastRepository, PodcastRepository>()
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
    .AddScoped<IYouTubeSearchService, YouTubeSearchService>()
    .AddSingleton<IAppleBearerTokenProvider>(new AppleBearerTokenProvider(args[1]))
    .AddSingleton(new JsonSerializerOptions
    {
        WriteIndented = true
    })
    .AddHttpClient<IApplePodcastService, ApplePodcastService>((services,httpClient) =>
    {
        var appleBearerTokenProvider = services.GetService<IAppleBearerTokenProvider>();
        httpClient.BaseAddress = new Uri("https://amp-api.podcasts.apple.com/");
        httpClient.DefaultRequestHeaders.Authorization = appleBearerTokenProvider!.GetHeader();
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
var processor = host.Services.GetService<UrlSubmitter>();
await processor!.Run(new Uri(args[0], UriKind.Absolute));