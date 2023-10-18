using System.Reflection;
using System.Text.Json;
using CommandLine;
using EnrichYouTubeOnlyPodcasts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Matching;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices.YouTube;
using RedditPodcastPoster.Persistence;


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
    .AddScoped<EnrichYouTubePodcastProcessor>()
    .AddScoped<IPodcastRepository, PodcastRepository>()
    .AddSingleton<IJsonSerializerOptionsProvider, JsonSerializerOptionsProvider>()
    .AddScoped<IEpisodeMatcher, EpisodeMatcher>()
    .AddScoped<IDataRepository, CosmosDbRepository>()
    .AddScoped<IYouTubePlaylistService, YouTubePlaylistService>()
    .AddScoped<IYouTubeChannelService, YouTubeChannelService>()
    .AddScoped<IYouTubeVideoService, YouTubeVideoService>()
    .AddScoped<IYouTubeChannelVideoSnippetsService, YouTubeChannelVideoSnippetsService>()
    .AddScoped<IYouTubeEpisodeProvider, YouTubeEpisodeProvider>()
    .AddScoped<IYouTubeItemResolver, YouTubeItemResolver>()
    .AddScoped<IYouTubeSearcher, YouTubeSearcher>();

CosmosDbClientFactory.AddCosmosClient(builder.Services);
YouTubeServiceFactory.AddYouTubeService(builder.Services);

builder.Services
    .AddOptions<CosmosDbSettings>().Bind(builder.Configuration.GetSection("cosmosdb"));
builder.Services
    .AddOptions<YouTubeSettings>().Bind(builder.Configuration.GetSection("youtube"));

using var host = builder.Build();

return await Parser.Default.ParseArguments<EnrichYouTubePodcastRequest>(args)
    .MapResult(async processRequest => await Run(processRequest), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(EnrichYouTubePodcastRequest request)
{
    var processor = host.Services.GetService<EnrichYouTubePodcastProcessor>();
    await processor!.Run(request);
    return 0;
}