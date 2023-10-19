using System.Reflection;
using System.Text.Json;
using CommandLine;
using EnrichYouTubeOnlyPodcasts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddYouTubeServices(builder.Configuration)
    .AddRepositories(builder.Configuration)
    .AddYouTubeServices(builder.Configuration)
    .AddSingleton<EnrichYouTubePodcastProcessor>();

using var host = builder.Build();

return await Parser.Default.ParseArguments<EnrichYouTubePodcastRequest>(args)
    .MapResult(async processRequest => await Run(processRequest), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(EnrichYouTubePodcastRequest request)
{
    var processor = host.Services.GetService<EnrichYouTubePodcastProcessor>();
    await processor!.Run(request);
    return 0;
}