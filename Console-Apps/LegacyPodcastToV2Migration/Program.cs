using LegacyPodcastToV2Migration;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Discovery.Extensions;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Persistence.Legacy.Extensions;
using RedditPodcastPoster.PushSubscriptions.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.KnownTerms;
using System.Net;
using System.Reflection;

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
    .AddLegacyCosmosDb()
    .AddLegacyPodcastRepository()
    .AddLegacyPushSubscriptionRepository()
    .AddLegacySubjectRepository()
    .AddLegacyDiscoveryResultsRepository()
    .AddPushSubscriptionsRepository()
    .AddSubjectServices()
    .AddDiscoveryRepository()
    .AddSingleton<IKnownTermsRepository, KnownTermsRepository>()
    .AddSingleton<LegacyPodcastToV2MigrationProcessor>();

// Support a teardown flag to delete target v2 containers before running migration/parity checks.
var teardown = builder.Configuration.GetValue<bool>("teardown", false);

if (teardown)
{
    var settings = builder.Configuration.GetSection("cosmosdbv2").Get<CosmosDbSettings>();
    if (settings == null)
    {
        Console.WriteLine("MigrationCosmosSettings not configured. Aborting teardown.");
        return;
    }

    // Create a temporary Cosmos client to perform container deletion
    var cosmosClientOptions = new CosmosClientOptions
    {
        AllowBulkExecution = true
    };
    if (settings.UseGateway == true)
    {
        cosmosClientOptions.ConnectionMode = ConnectionMode.Gateway;
    }

    using var cosmosClient = new CosmosClient(settings.Endpoint, settings.AuthKeyOrResourceToken, cosmosClientOptions);
    var database = cosmosClient.GetDatabase(settings.DatabaseId);

    // Dry-run is the default. Pass --teardownExecute true to perform destructive delete operations.
    var teardownExecute = builder.Configuration.GetValue<bool>("teardownExecute", false);
    if (!teardownExecute)
    {
        Console.WriteLine(
            "TEARDOWN: running in dry-run mode (no deletions). To execute deletions, pass --teardownExecute true.");
    }

    var containersToDelete = new[]
    {
        settings.PodcastsContainer,
        settings.EpisodesContainer,
        settings.SubjectsContainer,
        settings.DiscoveryContainer,
        settings.LookUpsContainer,
        settings.PushSubscriptionsContainer
    };

    foreach (var container in containersToDelete)
    {
        if (string.IsNullOrWhiteSpace(container))
        {
            continue;
        }

        try
        {
            var cont = database.GetContainer(container);
            var totalDocuments = await GetContainerDocumentCount(cont);
            var query = new QueryDefinition("SELECT c.id, c.podcastId FROM c");
            var iterator = cont.GetItemQueryIterator<dynamic>(query);
            var deleted = 0;
            var wouldDelete = 0;
            var failed = 0;
            var skipped = 0;
            var scanned = 0;
            var lastUpdateUtc = DateTime.MinValue;

            while (iterator.HasMoreResults)
            {
                var feed = await iterator.ReadNextAsync();
                scanned += feed.Count;
                var candidates = new List<(string Id, string PartitionKey)>();

                foreach (var doc in feed)
                {
                    string id = doc.id?.ToString();
                    var pkValue = doc.podcastId ?? doc.id;
                    string pk = pkValue?.ToString() ?? id;
                    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(pk))
                    {
                        skipped++;
                        continue;
                    }

                    candidates.Add((id, pk));
                }

                if (teardownExecute)
                {
                    var deleteTasks = candidates.Select(async candidate =>
                    {
                        try
                        {
                            await cont.DeleteItemAsync<dynamic>(candidate.Id, new PartitionKey(candidate.PartitionKey));
                            return true;
                        }
                        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                        {
                            return true;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to delete item '{candidate.Id}' from '{container}': {ex.Message}");
                            return false;
                        }
                    }).ToArray();

                    var deleteResults = await Task.WhenAll(deleteTasks);
                    deleted += deleteResults.Count(x => x);
                    failed += deleteResults.Count(x => !x);
                }
                else
                {
                    foreach (var candidate in candidates.Take(Math.Max(0, 10 - wouldDelete)))
                    {
                        Console.WriteLine(
                            $"[dry-run] would delete item id={candidate.Id} pk={candidate.PartitionKey} from container={container}");
                    }

                    wouldDelete += candidates.Count;
                }

                WriteProgress(
                    $"Teardown progress [{container}]: {scanned}/{totalDocuments} ({Percent(scanned, totalDocuments)}%)",
                    ref lastUpdateUtc,
                    force: scanned >= totalDocuments);
            }

            if (teardownExecute)
            {
                Console.WriteLine(
                    $"Deleted {deleted} documents from container: {container} (failed: {failed}, skipped: {skipped})");
            }
            else
            {
                Console.WriteLine(
                    $"[dry-run] Would delete approximately {wouldDelete} documents from container: {container} (skipped: {skipped})");
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            Console.WriteLine($"Container not found: {container}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to purge container '{container}': {ex.Message}");
        }
    }

    return;
}

using var host = builder.Build();

var processor = host.Services.GetRequiredService<LegacyPodcastToV2MigrationProcessor>();

var result = await processor.Run();
Console.WriteLine($"Podcasts migrated: {result.PodcastsMigrated}");
Console.WriteLine($"Episodes migrated: {result.EpisodesMigrated}");
Console.WriteLine($"Failed podcasts: {result.FailedPodcastIds.Count}");
Console.WriteLine($"Failed episodes: {result.FailedEpisodeIds.Count}");

var cosmosDbSettingsV2 = builder.Configuration.GetSection("cosmosdbv2").Get<CosmosDbSettings>();
if (cosmosDbSettingsV2 != null)
{
    var cosmosClientOptions = new CosmosClientOptions();
    if (cosmosDbSettingsV2.UseGateway == true)
    {
        cosmosClientOptions.ConnectionMode = ConnectionMode.Gateway;
    }

    using var verificationCosmosClient = new CosmosClient(
        cosmosDbSettingsV2.Endpoint,
        cosmosDbSettingsV2.AuthKeyOrResourceToken,
        cosmosClientOptions);
    var verificationDatabase = verificationCosmosClient.GetDatabase(cosmosDbSettingsV2.DatabaseId);
    var podcastsContainer = verificationDatabase.GetContainer(cosmosDbSettingsV2.PodcastsContainer);
    var episodesContainer = verificationDatabase.GetContainer(cosmosDbSettingsV2.EpisodesContainer);

    var persistedPodcastCount = await GetContainerDocumentCount(podcastsContainer);
    var persistedEpisodeCount = await GetContainerDocumentCount(episodesContainer);

    Console.WriteLine($"Persisted Podcasts count: {persistedPodcastCount}");
    Console.WriteLine($"Persisted Episodes count: {persistedEpisodeCount}");
}

var podcastParity = await processor.VerifySampledPodcastParity(1000);
Console.WriteLine($"Podcast parity sampled: {podcastParity.SampledCount}");
Console.WriteLine($"Podcast parity matches: {podcastParity.MatchingCount}");
Console.WriteLine($"Podcast parity missing in target: {podcastParity.MissingInTargetIds.Count}");
Console.WriteLine($"Podcast parity mismatches: {podcastParity.MismatchedIds.Count}");

var subjectParity = await processor.VerifySampledSubjectParity(1000);
PrintEntityParity(subjectParity);
var discoveryParity = await processor.VerifySampledDiscoveryParity(100);
PrintEntityParity(discoveryParity);
var lookupParity = await processor.VerifyLookupParity();
PrintEntityParity(lookupParity);
var pushParity = await processor.VerifySampledPushSubscriptionParity(25);
PrintEntityParity(pushParity);
var episodeParity = await processor.VerifySampledEpisodeParity(1000);
PrintEntityParity(episodeParity);


static void PrintEntityParity(EntityParityVerificationResult parity)
{
    Console.WriteLine($"{parity.EntityName} parity sampled: {parity.SampledCount}");
    Console.WriteLine($"{parity.EntityName} parity matches: {parity.MatchingCount}");
    Console.WriteLine($"{parity.EntityName} parity missing in target: {parity.MissingInTargetIds.Count}");
    Console.WriteLine($"{parity.EntityName} parity mismatches: {parity.MismatchedIds.Count}");
}

static async Task<int> GetContainerDocumentCount(Container container)
{
    var iterator = container.GetItemQueryIterator<int>(new QueryDefinition("SELECT VALUE COUNT(1) FROM c"));
    while (iterator.HasMoreResults)
    {
        var response = await iterator.ReadNextAsync();
        return response.FirstOrDefault();
    }

    return 0;
}

static int Percent(int current, int total)
{
    return current * 100 / (total == 0 ? 1 : total);
}

static void WriteProgress(string message, ref DateTime lastUpdateUtc, bool force = false)
{
    var now = DateTime.UtcNow;
    if (!force && now - lastUpdateUtc < TimeSpan.FromSeconds(5))
    {
        return;
    }

    var width = 120;
    try
    {
        width = Math.Max(Console.WindowWidth - 1, 20);
    }
    catch
    {
    }

    if (message.Length > width)
    {
        message = message[..width];
    }

    Console.Write($"\r{message.PadRight(width)}");
    if (force)
    {
        Console.WriteLine();
    }

    lastUpdateUtc = now;
}