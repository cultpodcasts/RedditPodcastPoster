using System.Reflection;
using CommandLine;
using MergeTvShows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Persistence.Extensions;

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

builder.Services
    .AddLogging()
    .AddRepositories()
    .AddEpisodeSearchIndexerService()
    .AddScoped<Processor>()
    .AddScoped<EnrichToxicEmpireMeta>()
    .AddHttpClient();

using var host = builder.Build();

return await Parser.Default.ParseArguments<MergeTvShowsRequest>(args)
    .MapResult(async request =>
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        if (request.EnrichToxicEmpireMeta)
        {
            var enricher = host.Services.GetRequiredService<EnrichToxicEmpireMeta>();
            return await enricher.Process(request.Apply, cts.Token);
        }

        var processor = host.Services.GetRequiredService<Processor>();
        return await processor.Process(request, cts.Token);
    },
    _ => Task.FromResult(-1));
