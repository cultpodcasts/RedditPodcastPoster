using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Azure;
using CommandLine;
using CreateSearchIndex;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Configuration;

if (VersionInfo.TryHandleVersionRequest(args))
{
    return 0;
}

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services.AddCreateSearchIndexConsoleServices();

using var host = builder.Build();
return await Parser.Default.ParseArguments<CreateSearchIndexRequest>(args)
    .MapResult(async request => await Run(request),
        errs => Task.FromResult(-1));

async Task<int> Run(CreateSearchIndexRequest request)
{
    var processor = host.Services.GetService<CreateSearchIndexProcessor>()!;
    // Non-zero when the film push after a teardown + run-indexer did not complete, so it is not missed in a long run.
    if (await processor.Process(request))
    {
        return 0;
    }

    Console.Error.WriteLine(
        "CreateSearchIndex: the index was rebuilt but the film push did not complete; run 'FilmSearchPusher --full'.");
    return 2;
}
