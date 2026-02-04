using System.Diagnostics;
using System.Reflection;
using iTunesSearch.Library;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.BBC.Extensions;
using RedditPodcastPoster.Cloudflare.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher.Extensions;
using RedditPodcastPoster.InternetArchive.Extensions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PushSubscriptions.Extensions;
using RedditPodcastPoster.Reddit.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;
using RedditPodcastPoster.UrlShortening;
using RedditPodcastPoster.UrlShortening.Extensions;
using RedditPodcastPoster.UrlSubmission.Extensions;

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
    .AddContentPublishing()
    .AddCloudflareClients()
    .AddTextSanitiser()
    .AddPodcastServices()
    .AddSubjectServices()
    .AddRedditServices()
    .AddSpotifyServices()
    .AddUrlSubmission()
    .AddBBCServices()
    .AddInternetArchiveServices()
    .AddAppleServices()
    .AddSpotifyServices()
    .AddYouTubeServices(ApplicationUsage.Api)
    .AddScoped(s => new iTunesSearchManager())
    .AddSubjectServices()
    .AddSubjectProvider()
    .AddPushSubscriptions()
    .AddShortnerServices()
    .AddHttpClient();


using var host = builder.Build();
var service = host.Services.GetRequiredService<IShortnerService>();

var repository = host.Services.GetRequiredService<IPodcastRepository>();
var guid = Guid.Parse(args[0]);
var podcast = await repository!.GetBy(x => x.Episodes.Any(y => y.Id == guid));
if (podcast == null)
{
    return;
}

var episode = podcast.Episodes.Single(x => x.Id == guid);
try
{
    var result = await service.Delete([new PodcastEpisode(podcast, episode)]);
}
catch (Exception e)
{
    Console.WriteLine($"Error occurred: {e.Message}");
}

return;

string GetBasePath()
{
    using var processModule = Process.GetCurrentProcess().MainModule;
    return Path.GetDirectoryName(processModule?.FileName) ?? throw new InvalidOperationException();
}