using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Cloudflare.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher;
using RedditPodcastPoster.ContentPublisher.Extensions;
using RedditPodcastPoster.ContentPublisher.Models;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Paginators;
using RedditPodcastPoster.Reddit.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;
using SpotifyAPI.Web;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration.SetBasePath(GetBasePath());

builder.Configuration
    .AddJsonFile("appsettings.json", false)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly())
    ;

builder.Services
    .AddLogging()
    .AddRepositories()
    .AddContentPublishing()
    .AddCloudflareClients()
    .AddTextSanitiser()
    .AddSubjectServices()
    .AddRedditServices()
    .AddSpotifyServices();


using var host = builder.Build();

var spotifyClient = host.Services.GetService<ISpotifyClientWrapper>();
var indexingContext = new IndexingContext()
{
    ReleasedSince = DateTimeExtensions.DaysAgo(2)
};
var showEpisodesRequest = new ShowEpisodesRequest()
{
    Limit = 1
};
var pagableSimpleEpisodes = await spotifyClient.GetShowEpisodes("1An7v0PYI2xPBjtvwEBvYm", showEpisodesRequest, indexingContext);
if (indexingContext.SkipSpotifyUrlResolving)
{
    throw new InvalidOperationException("Triggered Spotify Skip");
}
var component = host.Services.GetService<ISpotifyQueryPaginator>()!;
var result= await component.PaginateEpisodes(pagableSimpleEpisodes, indexingContext);

return;

string GetBasePath()
{
    using var processModule = Process.GetCurrentProcess().MainModule;
    return Path.GetDirectoryName(processModule?.FileName) ?? throw new InvalidOperationException();
}