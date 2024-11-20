using System.Reflection;
using AddYouTubeChannelAsPodcast;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Models;
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
    .AddRepositories()
    .AddYouTubeServices()
    .AddScoped<AddYouTubeChannelProcessor>()
    .AddScoped<IYouTubePlaylistService, YouTubePlaylistService>()
    .AddScoped<IYouTubeChannelService, YouTubeChannelService>()
    .AddCommonServices()
    .AddHttpClient();

using var host = builder.Build();

return await Parser.Default.ParseArguments<Args>(args)
    .MapResult(async processRequest => await Run(processRequest), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(Args request)
{
    var processor = host.Services.GetService<AddYouTubeChannelProcessor>();
    var result = await processor!.Run(request);
    if (result)
    {
        return 0;
    }

    return -1;
}