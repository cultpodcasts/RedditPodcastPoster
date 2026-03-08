using System.Reflection;
using LegacyPodcastToV2Migration;
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

using var host = builder.Build();

var processor = host.Services.GetRequiredService<LegacyPodcastToV2MigrationProcessor>();
var result = await processor.Run(new LegacyPodcastToV2MigrationSections(MigratePodcastsAndEpisodes: false, MigratePushSubscriptions:false, MigrateLookup:false, MigrateSubjects:false));

Console.WriteLine($"Podcasts migrated: {result.PodcastsMigrated}");
Console.WriteLine($"Episodes migrated: {result.EpisodesMigrated}");
Console.WriteLine($"Failed podcasts: {result.FailedPodcastIds.Count}");
Console.WriteLine($"Failed episodes: {result.FailedEpisodeIds.Count}");
