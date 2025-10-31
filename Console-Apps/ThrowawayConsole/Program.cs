using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.BBC;
using RedditPodcastPoster.BBC.Extensions;
using RedditPodcastPoster.Cloudflare.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.Reddit.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;
using System.Diagnostics;
using System.Reflection;

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
    .AddSpotifyServices()
    .AddBBCServices()
    .AddHttpClient();


using var host = builder.Build();

var service = host.Services.GetService<IBBCPageMetaDataExtractor>()!;

Uri.TryCreate(args[0], UriKind.Absolute, out var url);
var x=await service.GetMetaData(url!);

return;

string GetBasePath()
{
    using var processModule = Process.GetCurrentProcess().MainModule;
    return Path.GetDirectoryName(processModule?.FileName) ?? throw new InvalidOperationException();
}