// SP-2.2 (#1322): reconcile the D1 search index with Cosmos, and the initial backfill.
// Dry run unless --write is given. The index and credentials come from the d1Search section in local user secrets
// (see Console-Apps/README.md); the deployed Functions only ever know the production index.
using System.Reflection;
using CommandLine;
using D1SearchReconcile;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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
    var request = D1SearchReconcileRequest.FromCommandLine(options.Write, options.AllowDrift, out var error);
    if (request is null)
    {
        Console.Error.WriteLine(error);
        return -1;
    }

    using var scope = host.Services.CreateScope();
    var endpoint = scope.ServiceProvider.GetRequiredService<IOptions<D1SearchOptions>>().Value.Endpoint;
    Console.WriteLine($"D1 search index: {endpoint?.Host ?? "(not configured)"}; {(request.Write ? "WRITE" : "dry run")}.");
    var reconciler = scope.ServiceProvider.GetRequiredService<ID1SearchReconciler>();
    var r = await reconciler.Reconcile(request, CancellationToken.None);
    Console.WriteLine(
        $"{r.Outcome}. D1 {r.D1Documents}, eligible {r.Eligible}, missing {r.Missing}, stale {r.Stale}, " +
        $"orphans {r.Orphans}, not built {r.FailedToBuild}, drift {r.Drift:P2}; upserted {r.Upserted}, deleted {r.Deleted}." +
        (r.Error is null ? string.Empty : $" Error: {r.Error}"));
    if (r.Outcome == D1SearchReconcileOutcome.NotConfigured)
    {
        Console.Error.WriteLine(
            "The D1 search sync is not enabled and configured: set d1Search:Enabled, Endpoint, Auth0Domain, Audience, " +
            "ClientId and ClientSecret in user secrets.");
    }

    return D1SearchReconcileRequest.ExitCode(r);
}
