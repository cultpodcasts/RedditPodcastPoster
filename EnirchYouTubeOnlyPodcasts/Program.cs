using EnirchYouTubeOnlyPodcasts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Common.PodcastServices.YouTube;

if (args.Length != 1)
{
    throw new ArgumentNullException("Missing Spotify-Id");
}

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
    .AddScoped<EnrichYouTubePodcastProcessor>();

CosmosDbClientFactory.AddCosmosClient(builder.Services);
YouTubeServiceFactory.AddYouTubeService(builder.Services);

builder.Services
    .AddOptions<CosmosDbSettings>().Bind(builder.Configuration.GetSection("cosmosdb"));
builder.Services
    .AddOptions<YouTubeSettings>().Bind(builder.Configuration.GetSection("youtube"));

using var host = builder.Build();
var processor = host.Services.GetService<EnrichYouTubePodcastProcessor>();
await processor.Create(new PodcastCreateRequest(args[0]));