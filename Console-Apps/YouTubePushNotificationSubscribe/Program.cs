using System.Reflection;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.YouTubePushNotifications.Extensions;
using YouTubePushNotificationSubcribe;
using YouTubePushNotificationSubscribe;

var builder = Host.CreateApplicationBuilder(args);


builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddScoped<SubscribeProcessor>()
    .AddRepositories(builder.Configuration)
    .AddYouTubePushNotificationServices(builder.Configuration)
    .AddHttpClient();

using var host = builder.Build();
return await Parser.Default.ParseArguments<SubscribeRequest>(args)
    .MapResult(async request => await Run(request), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(SubscribeRequest request)
{
    var urlSubmitter = host.Services.GetService<SubscribeProcessor>()!;
    await urlSubmitter.Process(request);
    return 0;
}