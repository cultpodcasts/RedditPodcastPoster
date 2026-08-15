using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CommandLine;
using Discover;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Configuration;

var builder = Host.CreateApplicationBuilder(args);

var appDirectory = AppContext.BaseDirectory;
builder.Environment.ContentRootPath = appDirectory;

builder.Configuration
    .AddJsonFile(Path.Combine(appDirectory, "appsettings.json"), true)
    .AddJsonFile(Path.Combine(appDirectory, "Discover.appsettings.json"), true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services.AddLogging();
Ioc.ConfigureServices(builder.Services);

using var host = builder.Build();

return await Parser.Default.ParseArguments<DiscoveryRequest>(args)
    .MapResult(async discoveryRequest => await Run(discoveryRequest), errs =>
    {
        if (errs.Any(x => x is VersionRequestedError))
        {
            VersionInfo.PrintVersion();
            return Task.FromResult(0);
        }

        return Task.FromResult(-1);
    });

async Task<int> Run(DiscoveryRequest request)
{
    if (request.Version)
    {
        VersionInfo.PrintVersion();
        return 0;
    }

    var processor = host.Services.GetService<DiscoveryProcessor>()!;
    var result = await processor.Process(request);
    if (result.Initiation.HasValue)
    {
        var initiation = $"{result.Initiation:O}";
        Console.WriteLine($"Discovery initiated at '{initiation}'.");
    }

    return 0;
}
