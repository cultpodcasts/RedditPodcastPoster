using System.Reflection;
using CommandLine;
using EnrichExistingEpisodesFromPodcastServices;
using iTunesSearch.Library;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.Text.Extensions;
using RedditPodcastPoster.UrlSubmission.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddRepositories()
    .AddScoped<EnrichPodcastEpisodesProcessor>()
    .AddUrlSubmission()
    .AddCommonServices()
    .AddSpotifyServices()
    .AddYouTubeServices()
    .AddAppleServices()
    .AddRemoteClient()
    .AddTextSanitiser()
    .AddScoped(s => new iTunesSearchManager())
    .AddHttpClient();

using var host = builder.Build();

return await Parser.Default.ParseArguments<EnrichPodcastEpisodesRequest>(args)
    .MapResult(async request => await Run(request), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(EnrichPodcastEpisodesRequest request)
{
    var urlSubmitter = host.Services.GetService<EnrichPodcastEpisodesProcessor>()!;
    await urlSubmitter.Run(request);
    return 0;
}