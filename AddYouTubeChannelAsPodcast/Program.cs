using System.Reflection;
using AddYouTubeChannelAsPodcast;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Matching;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices.YouTube;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddScoped<IYouTubeChannelResolver, YouTubeChannelResolver>()
    .AddScoped<AddYouTubeChannelProcessor>()
    .AddScoped<IPodcastRepository, PodcastRepository>()
    .AddScoped<IEpisodeMatcher, EpisodeMatcher>()
    .AddScoped<IDataRepository, CosmosDbRepository>()
    .AddSingleton<ICosmosDbKeySelector, CosmosDbKeySelector>()
    .AddSingleton<PodcastFactory>()
    .AddScoped<IYouTubeSearchService, YouTubeSearchService>();

YouTubeServiceFactory.AddYouTubeService(builder.Services);
CosmosDbClientFactory.AddCosmosClient(builder.Services);

builder.Services
    .AddOptions<CosmosDbSettings>().Bind(builder.Configuration.GetSection("cosmosdb"));
builder.Services
    .AddOptions<YouTubeSettings>().Bind(builder.Configuration.GetSection("youtube"));

using var host = builder.Build();

return await Parser.Default.ParseArguments<Args>(args)
    .MapResult(async processRequest => await Run(processRequest), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(Args request)
{
    var processor = host.Services.GetService<AddYouTubeChannelProcessor>();
    var result = await processor.Run(request);
    if (result)
    {
        return 0;
    }

    return -1;
}