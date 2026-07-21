using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CommandLine;
using DiscoveryScoreBackfill;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Discovery.Extensions;
using RedditPodcastPoster.Discovery.ML.Configuration;
using RedditPodcastPoster.Discovery.ML.Services;
using RedditPodcastPoster.Persistence.Extensions;

var builder = Host.CreateApplicationBuilder(args);

var appDirectory = AppContext.BaseDirectory;
builder.Environment.ContentRootPath = appDirectory;

builder.Configuration
    .AddJsonFile(Path.Combine(appDirectory, "appsettings.json"), true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddRepositories()
    .AddDiscoveryRepository()
    .AddSingleton<IDiscoveryResultScorer, DiscoveryResultScorer>()
    .BindConfiguration<DiscoveryScorerSettings>("discover:scorer")
    .AddScoped<DiscoveryScoreBackfillProcessor>();

using var host = builder.Build();

return await Parser.Default.ParseArguments<DiscoveryScoreBackfillRequest>(args)
    .MapResult(
        async request =>
        {
            if (!request.AllUnprocessed && (request.DocumentIds == null || !request.DocumentIds.Any()))
            {
                request.AllUnprocessed = true;
            }

            var evidencePath = request.EvidencePath ?? ResolveDefaultEvidencePath();
            var processor = host.Services.GetRequiredService<DiscoveryScoreBackfillProcessor>();
            return await processor.Run(request, evidencePath);
        },
        _ => Task.FromResult(1));

static string ResolveDefaultEvidencePath()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory != null)
    {
        if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            return Path.Combine(directory.FullName, "docs", "discovery-scorer-backfill-evidence.md");
        }

        directory = directory.Parent;
    }

    return Path.Combine(Directory.GetCurrentDirectory(), "docs", "discovery-scorer-backfill-evidence.md");
}
