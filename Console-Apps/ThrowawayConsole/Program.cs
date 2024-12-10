using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.BBC;
using RedditPodcastPoster.BBC.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.InternetArchive.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Factories;
using RedditPodcastPoster.PodcastServices.YouTube.Video;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration.SetBasePath(GetBasePath());

builder.Configuration
    .AddJsonFile("appsettings.json", false)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddRepositories()
    .AddAppleServices()
    .AddYouTubeServices(ApplicationUsage.Cli)
    .AddSpotifyServices()
    .AddBBCServices()
    .AddInternetArchiveServices()
    .AddHttpClient();

builder.Services.AddPostingCriteria();
builder.Services.AddDelayedYouTubePublication();


using var host = builder.Build();

var component = host.Services.GetService<IYouTubeVideoService>();
var youTubeServiceFactroy = host.Services.GetService<IYouTubeServiceFactory>();
var youTubeService = youTubeServiceFactroy!.Create(ApplicationUsage.Cli);

var video = await component.GetVideoContentDetails(youTubeService!, new[] {"wI87U-TuZmo"}, new IndexingContext(), withSnippets:true);
var videoImage = video.SingleOrDefault()?.GetImageUrl();


return;

string GetBasePath()
{
    using var processModule = Process.GetCurrentProcess().MainModule;
    return Path.GetDirectoryName(processModule?.FileName) ?? throw new InvalidOperationException();
}