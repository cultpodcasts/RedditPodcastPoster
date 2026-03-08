using LegacyPodcastToV2Migration;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
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
    .AddPushSubscriptionsRepository()
    .AddSubjectServices()
    .AddDiscoveryRepository()
    .AddSingleton<IPushSubscriptionRepository, PushSubscriptionRepository>()
    .AddSingleton<IKnownTermsRepository, KnownTermsRepository>()
    .AddSingleton<LegacyPodcastToV2MigrationProcessor>();

using var host = builder.Build();

var processor = host.Services.GetRequiredService<LegacyPodcastToV2MigrationProcessor>();


if (false)
{
    var result = await processor.Run();
    Console.WriteLine($"Podcasts migrated: {result.PodcastsMigrated}");
    Console.WriteLine($"Episodes migrated: {result.EpisodesMigrated}");
    Console.WriteLine($"Failed podcasts: {result.FailedPodcastIds.Count}");
    Console.WriteLine($"Failed episodes: {result.FailedEpisodeIds.Count}");
}

var podcastParity = await processor.VerifySampledPodcastParity(sampleSize: 1000);
var subjectParity = await processor.VerifySampledSubjectParity(sampleSize: 1000);
var discoveryParity = await processor.VerifySampledDiscoveryParity(sampleSize: 100);
var lookupParity = await processor.VerifyLookupParity();
var pushParity = await processor.VerifySampledPushSubscriptionParity(sampleSize: 25);
var episodeParity = await processor.VerifySampledEpisodeParity(sampleSize: 1000);

Console.WriteLine($"Podcast parity sampled: {podcastParity.SampledCount}");
Console.WriteLine($"Podcast parity matches: {podcastParity.MatchingCount}");
Console.WriteLine($"Podcast parity missing in target: {podcastParity.MissingInTargetIds.Count}");
Console.WriteLine($"Podcast parity mismatches: {podcastParity.MismatchedIds.Count}");

PrintEntityParity(subjectParity);
PrintEntityParity(discoveryParity);
PrintEntityParity(lookupParity);
PrintEntityParity(pushParity);
PrintEntityParity(episodeParity);

static void PrintEntityParity(EntityParityVerificationResult parity)
{
    Console.WriteLine($"{parity.EntityName} parity sampled: {parity.SampledCount}");
    Console.WriteLine($"{parity.EntityName} parity matches: {parity.MatchingCount}");
    Console.WriteLine($"{parity.EntityName} parity missing in target: {parity.MissingInTargetIds.Count}");
    Console.WriteLine($"{parity.EntityName} parity mismatches: {parity.MismatchedIds.Count}");
}
