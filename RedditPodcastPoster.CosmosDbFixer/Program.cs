using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using RedditPodcastPoster.CosmosDbFixer;

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
    .AddSingleton(new JsonSerializerOptions
    {
        WriteIndented = true
    })
    .AddScoped<CosmosDbFixer>()
    .AddScoped<ICosmosDbKeySelector, CosmosDbKeySelector>()
    .AddScoped<IPodcastRepository, PodcastRepository>()
    .AddScoped<ICachedSpotifyClient, CachedSpotifyClient>();

CosmosDbClientFactory.AddCosmosClient(builder.Services);
SpotifyClientFactory.AddSpotifyClient(builder.Services);

builder.Services
    .AddOptions<CosmosDbSettings>().Bind(builder.Configuration.GetSection("cosmosdb"));
builder.Services
    .AddOptions<SpotifySettings>().Bind(builder.Configuration.GetSection("spotify"));


using var host = builder.Build();
var processor = host.Services.GetService<CosmosDbFixer>();
await processor!.Run();