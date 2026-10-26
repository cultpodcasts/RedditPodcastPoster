using System.Reflection;
using CataloguePlayableServicesBackfill;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.StreamingCatalog;

if (VersionInfo.TryHandleVersionRequest(args))
{
    return 0;
}

StreamingCatalogLoader.EnsureLoaded();

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
    .AddSingleton(TimeProvider.System)
    .AddSingleton<IEpisodeBackupStore, FileEpisodeBackupStore>()
    .AddSingleton<Func<string, IPublicDatabaseUrlIndex>>(sp =>
    {
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(FilePublicDatabaseUrlIndex));
        return dir => FilePublicDatabaseUrlIndex.Load(dir, logger);
    })
    .AddScoped<CataloguePlayableServicesBackfillProcessor>()
    // Resolved only on --apply so a dry-run never builds the Search client.
    .AddScoped<Func<IPlayableSearchDocumentSwap>>(s => s.GetRequiredService<IPlayableSearchDocumentSwap>);

using var host = builder.Build();
return await Parser.Default.ParseArguments<CataloguePlayableServicesBackfillRequest>(args)
    .MapResult(async request => await Run(request), errs => Task.FromResult(1));

async Task<int> Run(CataloguePlayableServicesBackfillRequest request)
{
    using var scope = host.Services.CreateScope();
    var processor = scope.ServiceProvider.GetRequiredService<CataloguePlayableServicesBackfillProcessor>();
    var result = await processor.Run(request);
    return result.ExitCode;
}
