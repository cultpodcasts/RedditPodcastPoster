using System.Reflection;
using System.Text.Json;
using AddAudioPodcast;
using iTunesSearch.Library;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices.Apple;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using PodcastFactory = RedditPodcastPoster.Common.Podcasts.PodcastFactory;

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
    .AddScoped<AddAudioPodcast.PodcastFactory>()
    .AddScoped<IPodcastRepository, PodcastRepository>()
    .AddScoped<IDataRepository, CosmosDbRepository>()
    .AddSingleton<ICosmosDbKeySelector, CosmosDbKeySelector>()
    .AddSingleton<PodcastFactory>()
    .AddScoped<IApplePodcastEnricher, ApplePodcastEnricher>()
    .AddScoped<IApplePodcastResolver, ApplePodcastResolver>()
    .AddScoped(s => new iTunesSearchManager())
    .AddScoped<IRemoteClient, RemoteClient>()
    .AddScoped<IApplePodcastEnricher, ApplePodcastEnricher>()
    .AddHttpClient<RemoteClient>();

CosmosDbClientFactory.AddCosmosClient(builder.Services);
SpotifyClientFactory.AddSpotifyClient(builder.Services);

builder.Services
    .AddOptions<CosmosDbSettings>().Bind(builder.Configuration.GetSection("cosmosdb"));
builder.Services
    .AddOptions<SpotifySettings>().Bind(builder.Configuration.GetSection("spotify"));

using var host = builder.Build();
var processor = host.Services.GetService<AddAudioPodcast.PodcastFactory>();
await processor.Create(new PodcastCreateRequest(args[0]));