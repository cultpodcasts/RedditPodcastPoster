using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.InternetArchive;
using RedditPodcastPoster.InternetArchive.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelVideos;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Episode;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using System.Diagnostics;
using System.Reflection;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .SetBasePath(GetBasePath())
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddEpisodesDomain()
    .AddRepositories()
    .AddYouTubeServices(ApplicationUsage.Cli)
    .AddHttpClient();

using var host = builder.Build();

if (args.Length == 0 )
{
    Console.Error.WriteLine("Usage: ThrowawayConsole <internet-archive-url>");
    return 1;
}

var service = host.Services.GetRequiredService<IYouTubeServiceWrapper>();
var service2 = host.Services.GetRequiredService<IYouTubeVideoService>();
var service3 = host.Services.GetRequiredService<IYouTubeThumbnailResolver>();

var video = await service2.GetVideoContentDetails(service, [args[0]], new IndexingContext(), withSnippets: true);
var image= await service3.GetImageUrlAsync(video?.SingleOrDefault());


return 0;

string GetBasePath()
{
    using var processModule = Process.GetCurrentProcess().MainModule;
    return Path.GetDirectoryName(processModule?.FileName) ?? throw new InvalidOperationException();
}
