using System.Reflection;
using CommandLine;
using Discover;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.PodcastServices.ListenNotes.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.Text.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddScoped<DiscoveryProcessor>()
    .AddListenNotesClient(builder.Configuration)
    .AddScoped<ISpotifySearcher, SpotifySearcher>()
    .AddScoped<IListenNotesSearcher, ListenNotesSearcher>()
    .AddSpotifyServices(builder.Configuration)
    .AddTextSanitiser()
    .AddHttpClient();

using var host = builder.Build();
return await Parser.Default.ParseArguments<DiscoveryRequest>(args)
    .MapResult(async request => await Run(request), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(DiscoveryRequest request)
{
    var urlSubmitter = host.Services.GetService<DiscoveryProcessor>()!;
    await urlSubmitter.Process(request);
    return 0;
}