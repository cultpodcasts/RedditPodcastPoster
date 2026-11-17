// SP-2.2 (#1322): reconcile the D1 search index(es) with Cosmos, and the initial backfill.
// Dry run unless --write --target <name> is given. Credentials: the d1Search section (see docs/search-platform/sync-and-replication.md).
using System.Reflection;
using CommandLine;
using D1SearchReconcile;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.D1Search;

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

builder.Services.AddD1SearchReconcileConsoleServices();

using var host = builder.Build();

return await Parser.Default.ParseArguments<D1SearchReconcileRequestOptions>(args)
    .MapResult(async options => await Run(options),
        _ => Task.FromResult(-1));

async Task<int> Run(D1SearchReconcileRequestOptions options)
{
    var request = D1SearchReconcileRequest.FromCommandLine(options.Targets, options.Write, options.AllowDrift, out var error);
    if (request is null)
    {
        Console.Error.WriteLine(error);
        return -1;
    }

    using var scope = host.Services.CreateScope();
    var reconciler = scope.ServiceProvider.GetRequiredService<ID1SearchReconciler>();
    var reports = await reconciler.Reconcile(request, CancellationToken.None);
    foreach (var r in reports)
    {
        Console.WriteLine(
            $"{r.Target}: {r.Outcome}. D1 {r.D1Documents}, eligible {r.Eligible}, missing {r.Missing}, stale {r.Stale}, " +
            $"orphans {r.Orphans}, not built {r.FailedToBuild}, drift {r.Drift:P2}; upserted {r.Upserted}, deleted {r.Deleted}." +
            (r.Error is null ? string.Empty : $" Error: {r.Error}"));
    }

    if (reports.Count == 0)
    {
        Console.Error.WriteLine("No D1 search target is enabled and configured (d1Search:Targets).");
    }

    return D1SearchReconcileRequest.ExitCode(reports);
}
