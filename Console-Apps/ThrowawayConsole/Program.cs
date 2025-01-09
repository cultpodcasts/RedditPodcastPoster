using System.Diagnostics;
using System.Reflection;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Bluesky;
using RedditPodcastPoster.Bluesky.Extensions;
using RedditPodcastPoster.Cloudflare.Extensions;
using RedditPodcastPoster.CloudflareRedirect;
using RedditPodcastPoster.CloudflareRedirect.Extensions;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Extensions;
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
    .AddDelayedYouTubePublication();


using var host = builder.Build();

var repository = host.Services.GetService<IPodcastRepository>()!;
var component = host.Services.GetService<IBlueskyPostManager>()!;

var episodeId = new Guid(args[0]);

var podcastId = await repository.GetBy(x =>
    (!x.Removed.IsDefined() || x.Removed == false) &&
    x.Episodes.Any(ep => ep.Id == episodeId), x => new { guid = x.Id });
if (podcastId == null)
{
    throw new ArgumentException($"Episode with id '{episodeId}' not found.");
}
var podcast = await repository.GetBy(x => x.Id == podcastId.guid);
var episode= podcast!.Episodes.First(ep => ep.Id == episodeId);

await component.RemovePost(new RedditPodcastPoster.Models.PodcastEpisode(podcast, episode));

return;

string GetBasePath()
{
    using var processModule = Process.GetCurrentProcess().MainModule;
    return Path.GetDirectoryName(processModule?.FileName) ?? throw new InvalidOperationException();
}