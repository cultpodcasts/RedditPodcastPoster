using System.Reflection;
using CommandLine;
using iTunesSearch.Library;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.Reddit.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", false)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddRepositories()
    .AddPodcastServices()
    .AddTextSanitiser()
    .AddYouTubeServices(ApplicationUsage.Cli)
    .AddSpotifyServices()
    .AddAppleServices()
    .AddCommonServices()
    .AddSubjectServices()
    .AddCachedSubjectProvider()
    .AddRemoteClient()
    .AddScoped(s => new iTunesSearchManager())
    .AddEliminationTerms()
    .AddRedditServices()
    .AddScoped<PodcastProcessor>()
    .AddHttpClient();

builder.Services.AddDelayedYouTubePublication();

using var host = builder.Build();

var logger = host.Services.GetService<ILogger<Program>>();

return await Parser.Default.ParseArguments<ProcessRequest>(args)
    .MapResult(async processRequest => await Run(processRequest), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(ProcessRequest request)
{
    logger!.LogInformation($"{nameof(Run)} initiated.");
    var podcastProcessor = host.Services.GetService<PodcastProcessor>()!;
    var result = await podcastProcessor.Process(request);
    logger!.LogInformation($"{nameof(Run)} Operation results: '{result}'.");
    logger!.LogInformation($"{nameof(Run)} complete.");
    return result.ToResultCode();
}