using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;

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
    .AddYouTubeServices(ApplicationUsage.Indexer)
    .AddHttpClient();

builder.Services.AddPostingCriteria();
builder.Services.AddDelayedYouTubePublication();


using var host = builder.Build();

var component = host.Services.GetService<ITolerantYouTubeChannelVideoSnippetsService>()!;

try
{
    var y = await component.GetLatestChannelVideoSnippets(new YouTubeChannelId("UCWXWLynI5YwDHn_U-boE2Cg"),
        new IndexingContext() {ReleasedSince = DateTime.UtcNow - TimeSpan.FromHours(1)});
}
catch (Exception e)
{
    var x = 1;
}

return;

string GetBasePath()
{
    using var processModule = Process.GetCurrentProcess().MainModule;
    return Path.GetDirectoryName(processModule?.FileName) ?? throw new InvalidOperationException();
}