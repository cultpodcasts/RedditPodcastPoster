using System.Reflection;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Extensions;
using RedditPodcastPoster.YouTubePushNotifications.Extensions;
using WebsubStatus;

var builder = Host.CreateApplicationBuilder(args);


builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddScoped<WebSubStatusProcessor>()
    .AddRepositories()
    .AddPodcastServices()
    .AddCommonServices()
    .AddYouTubePushNotificationServices()
    .AddHttpClient();


using var host = builder.Build();
return await Parser.Default.ParseArguments<WebSubStatusRequest>(args)
    .MapResult(async request => await Run(request), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(WebSubStatusRequest request)
{
    var processor = host.Services.GetService<WebSubStatusProcessor>()!;
    await processor.Process(request);
    return 0;
}