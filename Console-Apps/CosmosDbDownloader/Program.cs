using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CommandLine;
using CosmosDbDownloader;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Discovery.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PushSubscriptions.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Configuration;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddRepositories()
    .AddSubjectServices()
    .AddDiscoveryRepository()
    .AddPushSubscriptionsRepository()
    .AddFileRepository(string.Empty, true)
    .AddSafeFileWriter()
    .AddSingleton<CosmosDbDownloader.CosmosDbDownloader>();

using var host = builder.Build();

return await Parser.Default.ParseArguments<CosmosDbDownloaderRequest>(args)
    .MapResult(async request =>
    {
        if (request.Version)
        {
            VersionInfo.PrintVersion();
            return 0;
        }

        var downloader = host.Services.GetRequiredService<CosmosDbDownloader.CosmosDbDownloader>();
        await downloader.Run(request);
        return 0;
    },
    errs =>
    {
        if (errs.Any(x => x is VersionRequestedError))
        {
            VersionInfo.PrintVersion();
            return Task.FromResult(0);
        }

        return Task.FromResult(1);
    });
