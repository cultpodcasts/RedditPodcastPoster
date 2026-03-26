using System.Reflection;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Discovery.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Persistence.Legacy.Extensions;
using RedditPodcastPoster.PushSubscriptions.Extensions;
using RedditPodcastPoster.Subjects.Extensions;

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
    .AddLegacyCosmosDb()
    .AddSubjectServices()
    .AddDiscoveryRepository()
    .AddPushSubscriptionsRepository()
    .AddFileRepository(string.Empty, true)
    .AddSafeFileWriter()
    .AddSingleton<CosmosDbDownloader.CosmosDbDownloader>()
    .AddSingleton<CosmosDbDownloader.CosmosDbDownloaderV2>();

using var host = builder.Build();

return await Parser.Default.ParseArguments<CosmosDbDownloader.DownloaderRequest>(args)
    .MapResult(async request => await Run(request),
        errs => Task.FromResult(-1));

async Task<int> Run(CosmosDbDownloader.DownloaderRequest request)
{
    if (request.UseV2)
    {
        var downloader = host.Services.GetRequiredService<CosmosDbDownloader.CosmosDbDownloaderV2>();
        await downloader.Run();
    }
    else
    {
        var downloader = host.Services.GetRequiredService<CosmosDbDownloader.CosmosDbDownloader>();
        await downloader.Run();
    }

    return 0;
}