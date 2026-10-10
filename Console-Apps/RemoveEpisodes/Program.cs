using System.Reflection;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Cloudflare.Extensions;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.UrlShortening.Extensions;
using RemoveEpisodes;
using RemoveEpisodes.PodcastRestore;

if (args.Contains("--version"))
{
    VersionInfo.PrintVersion();
    return 0;
}

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddScoped<Processor>()
    .AddScoped<RestoreProcessor>()
    .AddScoped<PodcastTargetResolver>()
    .AddScoped<RestorePodcastProcessor>()
    .AddCloudflareClients()
    .AddShortnerServices()
    .AddRepositories()
    .AddEpisodeSearchIndexerService()
    .AddHttpClient();

using var host = builder.Build();

return await Parser.Default.ParseArguments<RemoveRequest, RestoreRequest, RestorePodcastRequest>(args)
    .MapResult(
        (RemoveRequest request) => RunRemove(request),
        (RestoreRequest request) => RunRestore(request),
        (RestorePodcastRequest request) => RunRestorePodcast(request),
        errs => Task.FromResult(-1));

async Task<int> RunRemove(RemoveRequest request)
{
    var processor = host.Services.GetRequiredService<Processor>();
    await processor.Process(request);
    return 0;
}

async Task<int> RunRestore(RestoreRequest request)
{
    var processor = host.Services.GetRequiredService<RestoreProcessor>();
    await processor.Process(request);
    return 0;
}

async Task<int> RunRestorePodcast(RestorePodcastRequest request)
{
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };
    var processor = host.Services.GetRequiredService<RestorePodcastProcessor>();
    try
    {
        return await processor.Process(request, cts.Token);
    }
    catch (OperationCanceledException)
    {
        Console.Error.WriteLine("Cancelled. Changes made so far are kept; re-run with the same arguments to finish.");
        return 1;
    }
}
