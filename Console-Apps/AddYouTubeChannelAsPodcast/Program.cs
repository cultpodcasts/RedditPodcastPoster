using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AddYouTubeChannelAsPodcast;
using CommandLine;
using RedditPodcastPoster.Catalogue.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Channel;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.Configuration;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddEpisodesDomain()
    .AddRepositories()
    .AddYouTubeServices(ApplicationUsage.Cli)
    .AddScoped<AddYouTubeChannelProcessor>()
    .AddScoped<IYouTubePlaylistService, YouTubePlaylistService>()
    .AddScoped<IYouTubeChannelService, YouTubeChannelService>()
    .AddCatalogueServices()
    .AddHttpClient();

using var host = builder.Build();

return await Parser.Default.ParseArguments<Args>(args)
    .MapResult(async processRequest => await Run(processRequest), errs =>
    {
        if (errs.Any(x => x is VersionRequestedError))
        {
            VersionInfo.PrintVersion();
            return Task.FromResult(0);
        }

        return Task.FromResult(-1);
    });

async Task<int> Run(Args request)
{
    if (request.Version)
    {
        VersionInfo.PrintVersion();
        return 0;
    }

    var processor = host.Services.GetService<AddYouTubeChannelProcessor>();
    var result = await processor!.Run(request);
    if (result)
    {
        return 0;
    }

    return -1;
}