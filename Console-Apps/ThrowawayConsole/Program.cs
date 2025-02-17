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
using RedditPodcastPoster.Reddit.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;

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
    .AddRedditServices();


using var host = builder.Build();

var component = host.Services.GetService<IContentPublisher>()!;
await component.PublishDiscoveryInfo(new DiscoveryInfo {DocumentCount = 0});

return;

string GetBasePath()
{
    using var processModule = Process.GetCurrentProcess().MainModule;
    return Path.GetDirectoryName(processModule?.FileName) ?? throw new InvalidOperationException();
}