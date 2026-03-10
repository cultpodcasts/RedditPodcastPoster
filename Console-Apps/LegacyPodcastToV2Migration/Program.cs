using System.Net;
using System.Reflection;
using LegacyPodcastToV2Migration;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Discovery.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PushSubscriptions;
using RedditPodcastPoster.PushSubscriptions.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.KnownTerms;

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
    .AddPushSubscriptionsRepository()
    .AddSubjectServices()
    .AddDiscoveryRepository()
    .AddSingleton<IPushSubscriptionRepository, PushSubscriptionRepository>()
    .AddSingleton<IKnownTermsRepository, KnownTermsRepository>()
    .AddSingleton<LegacyPodcastToV2MigrationProcessor>();

// Support a teardown flag to delete target v2 containers before running migration/parity checks.
var teardown = builder.Configuration.GetValue<bool>("teardown", false);

if (teardown)
{
    var settings = builder.Configuration.GetSection("MigrationCosmosSettings").Get<MigrationCosmosSettings>();
    if (settings == null)
    {
        Console.WriteLine("MigrationCosmosSettings not configured. Aborting teardown.");
        return;
    }

    // Create a temporary Cosmos client to perform container deletion
    using var cosmosClient = new CosmosClient(settings.Endpoint, settings.AuthKeyOrResourceToken);
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
            // Instead of deleting the container resource, enumerate documents inside the v2 container.
            var cont = database.GetContainer(container);
            var query = new QueryDefinition("SELECT c.id, c.podcastId FROM c");
            var iterator = cont.GetItemQueryIterator<dynamic>(query);
            var deleted = 0;
            var wouldDelete = 0;
            while (iterator.HasMoreResults)
            {
                var feed = await iterator.ReadNextAsync();
                foreach (var doc in feed)
                {
                    try
                    {
                        // Attempt to resolve partition key: prefer podcastId, fall back to id
                        string id = doc.id?.ToString();
                        var pkValue = doc.podcastId ?? doc.id;
                        string pk = pkValue?.ToString() ?? id;
                        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(pk))
                        {
                            Console.WriteLine($"Skipping document with missing id/partition in container {container}.");
                            continue;
                        }

                        if (teardownExecute)
                        {
                            await cont.DeleteItemAsync<dynamic>(id, new PartitionKey(pk));
                            deleted++;
                        }
                        else
                        {
                            // Dry-run: record what would be deleted and print a short sample
                            wouldDelete++;
                            if (wouldDelete <= 10)
                            {
                                Console.WriteLine(
                                    $"[dry-run] would delete item id={id} pk={pk} from container={container}");
                            }
                        }
                    }
                    catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                    {
                        // idempotent - item already missing
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to process item in '{container}': {ex.Message}");
                    }
                }
            }

            if (teardownExecute)
            {
                Console.WriteLine($"Deleted {deleted} documents from container: {container}");
            }
            else
            {
                Console.WriteLine(
                    $"[dry-run] Would delete approximately {wouldDelete} documents from container: {container}");
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