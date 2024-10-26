using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PushSubscriptions;
using RedditPodcastPoster.PushSubscriptions.Extensions;

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
    .AddPushSubscriptions(builder.Configuration)
    .AddHttpClient();

builder.Services.AddPostingCriteria(builder.Configuration);
builder.Services.AddDelayedYouTubePublication(builder.Configuration);


using var host = builder.Build();

var publisher = host.Services.GetService<INotificationPublisher>()!;
await publisher.SendDiscoveryNotification();
return 0;

string GetBasePath()
{
    using var processModule = Process.GetCurrentProcess().MainModule;
    return Path.GetDirectoryName(processModule?.FileName) ?? throw new InvalidOperationException();
}