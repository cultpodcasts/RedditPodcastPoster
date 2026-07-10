using System.Net;
using System.Reflection;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Cloudflare.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.People.Extensions;
namespace PeopleMigrator;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
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
            .AddPeopleServices()
            .AddContentPublishing()
            .AddCloudflareClients()
            .AddHttpClient<IPersonDisplayNameResolver, PersonDisplayNameResolver>()
            .ConfigurePrimaryHttpMessageHandler(CreateHttpHandler)
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.TryAddWithoutValidation(
                    "User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            })
            .Services
            .AddScoped<PeopleMigrationProcessor>()
            .AddScoped<AliasEnrichmentProcessor>();

        using var host = builder.Build();
        return await Parser.Default.ParseArguments<PeopleMigrationRequest>(args)
            .MapResult(async request => await Run(host, request), _ => Task.FromResult(-1));
    }

    private static HttpMessageHandler CreateHttpHandler()
    {
        var handler = new SocketsHttpHandler();
        var proxyUrl = PersonDisplayNameResolver.ResolveConfiguredProxyUrl();
        if (!string.IsNullOrWhiteSpace(proxyUrl))
        {
            handler.Proxy = new WebProxy(proxyUrl);
            handler.UseProxy = true;
        }

        return handler;
    }

    private static async Task<int> Run(IHost host, PeopleMigrationRequest request)
    {
        if (request.IsReviewServer)
        {
            await PeopleReviewServer.RunAsync(host, request);
            return 0;
        }

        if (request.IsMergeSeedOnly)
        {
            var inputPath = Path.GetFullPath(request.MergeSeedFrom!);
            var outputPath = !string.IsNullOrWhiteSpace(request.OutputPath)
                ? Path.GetFullPath(request.OutputPath)
                : inputPath;

            var result = await PeopleSeedMerger.MergeSeedFileAsync(inputPath, outputPath);
            Console.WriteLine(
                "Merged {0} duplicate pair(s): {1} -> {2} person record(s). Wrote {3}.",
                result.Merged.Count,
                result.InputCount,
                result.OutputCount,
                result.OutputPath);

            foreach (var entry in result.Merged)
            {
                Console.WriteLine(
                    "  - {0}: {1} | twitter={2} bluesky={3} | episodes={4}",
                    entry.Label,
                    entry.SurvivorName,
                    entry.Twitter ?? "(none)",
                    entry.Bluesky ?? "(none)",
                    entry.EpisodeCount);
            }

            if (result.Skipped.Count > 0)
            {
                Console.WriteLine("Skipped:");
                foreach (var skip in result.Skipped)
                {
                    Console.WriteLine("  - {0}", skip);
                }
            }

            return 0;
        }

        if (request.IsCleanSeedOnly)
        {
            var inputPath = Path.GetFullPath(request.CleanSeedFrom!);
            var outputPath = !string.IsNullOrWhiteSpace(request.OutputPath)
                ? Path.GetFullPath(request.OutputPath)
                : inputPath;

            var result = await PeopleSeedJsonWriter.CleanSeedFileAsync(inputPath, outputPath);
            Console.WriteLine(
                "Promoted {0} canonical name(s) and cleaned {1} noisy/duplicate alias(es) across {2} person record(s). Wrote {3}.",
                result.CanonicalsPromoted,
                result.AliasesRemoved,
                result.PeopleCount,
                outputPath);

            if (result.PromotionExamples.Count > 0)
            {
                Console.WriteLine("Canonical promotion examples:");
                foreach (var example in result.PromotionExamples.Take(25))
                {
                    Console.WriteLine("  - {0}", example);
                }

                if (result.PromotionExamples.Count > 25)
                {
                    Console.WriteLine("  ... and {0} more", result.PromotionExamples.Count - 25);
                }
            }

            if (result.RemovedExamples.Count > 0)
            {
                Console.WriteLine("Removed alias examples:");
                foreach (var example in result.RemovedExamples.Take(25))
                {
                    Console.WriteLine("  - {0}", example);
                }

                if (result.RemovedExamples.Count > 25)
                {
                    Console.WriteLine("  ... and {0} more", result.RemovedExamples.Count - 25);
                }
            }

            return 0;
        }

        if (request.IsAliasEnrichmentOnly)
        {
            var aliasProcessor = host.Services.GetRequiredService<AliasEnrichmentProcessor>();
            var result = await aliasProcessor.RunAsync(request);
            Console.WriteLine(
                "Alias enrichment: processed {0}/{1} episode(s), added {2} alias(es) for {3} person(s). Wrote {4}.",
                result.EpisodesProcessed,
                result.EpisodesTotal,
                result.AliasesAdded,
                result.PeopleWithNewAliases,
                result.OutputPath);
            return 0;
        }

        var processor = host.Services.GetRequiredService<PeopleMigrationProcessor>();
        await processor.Run(request);
        return 0;
    }
}