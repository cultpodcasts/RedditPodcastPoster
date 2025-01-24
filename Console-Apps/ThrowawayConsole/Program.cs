using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.BBC;
using RedditPodcastPoster.BBC.Extensions;
using RedditPodcastPoster.Bluesky.Extensions;
using RedditPodcastPoster.Cloudflare.Extensions;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;
using RedditPodcastPoster.UrlShortening.Extensions;

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
    .AddBBCServices()
    .AddRepositories()
    .AddBlueskyServices()
    .AddHttpClient()
    .AddPostingCriteria()
    .AddTextSanitiser()
    .AddSubjectServices()
    .AddSpotifyServices()
    .AddYouTubeServices(ApplicationUsage.Cli)
    .AddCommonServices()
    .AddShortnerServices()
    .AddCloudflareClients()
    .AddAppleServices()
    .AddDelayedYouTubePublication();


using var host = builder.Build();

var repository= host.Services.GetService<IPodcastRepository>()!;
var podcast = await repository.GetPodcast(Guid.Parse(args[0]));
var component = host.Services.GetService<IAppleEpisodeRetrievalHandler>()!;
var indexingContext = new IndexingContext(DateTime.Now.Subtract(TimeSpan.FromDays(2)));


var (episodes, handled) = await component.GetEpisodes(podcast, indexingContext);

return;

string GetBasePath()
{
    using var processModule = Process.GetCurrentProcess().MainModule;
    return Path.GetDirectoryName(processModule?.FileName) ?? throw new InvalidOperationException();
}