using System.Diagnostics;
using System.Reflection;
using CommandLine;
using CosmosDbUploader;
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

builder.Configuration.SetBasePath(GetBasePath());

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddFileRepository(string.Empty, true)
    .AddRepositories()
    .AddLegacyCosmosDb()
    .AddSubjectServices()
    .AddDiscoveryRepository()
    .AddPushSubscriptionsRepository()
    .AddSingleton<CosmosDbUploader.CosmosDbUploader>()
    .AddSingleton<CosmosDbUploaderV2>();

using var host = builder.Build();

return await Parser.Default.ParseArguments<UploaderRequest>(args)
    .MapResult(async request => await Run(request),
        errs => Task.FromResult(-1));

async Task<int> Run(UploaderRequest request)
{
    if (request.UseV2)
    {
        var uploader = host.Services.GetRequiredService<CosmosDbUploaderV2>();
        await uploader.Run();
    }
    else
    {
        var uploader = host.Services.GetRequiredService<CosmosDbUploader.CosmosDbUploader>();
        await uploader.Run();
    }

    return 0;
}

string GetBasePath()
{
    using var processModule = Process.GetCurrentProcess().MainModule;
    return Path.GetDirectoryName(processModule?.FileName) ?? throw new InvalidOperationException();
}