using System.Reflection;
using CatalogueMigrate;
using CommandLine;
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
    .AddSingleton(TimeProvider.System)
    .AddSingleton<ICatalogueMigrateEvidenceStore, FileCatalogueMigrateEvidenceStore>()
    .AddScoped<ICatalogueMigrateDestination, RepositoryCatalogueMigrateDestination>()
    .AddScoped<CatalogueMigrateApplier>()
    // Resolved only on --apply / --rollback, so a dry-run never builds the Search client.
    .AddScoped<Func<CatalogueMigrateApplier>>(s => s.GetRequiredService<CatalogueMigrateApplier>)
    .AddScoped<CatalogueMigrateProcessor>();

using var host = builder.Build();
return await Parser.Default.ParseArguments<CatalogueMigrateRequest>(args)
    .MapResult(async request => await Run(request), errs => Task.FromResult(-1));

async Task<int> Run(CatalogueMigrateRequest request)
{
    using var scope = host.Services.CreateScope();
    var processor = scope.ServiceProvider.GetRequiredService<CatalogueMigrateProcessor>();
    var result = await processor.Run(request);
    return result.ExitCode;
}
