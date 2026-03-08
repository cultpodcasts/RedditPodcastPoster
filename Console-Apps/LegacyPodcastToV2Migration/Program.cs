using System.Reflection;
using LegacyPodcastToV2Migration;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Persistence.Abstractions;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .Configure<MigrationCosmosSettings>("legacy", builder.Configuration.GetSection("legacyCosmosdb"))
    .Configure<MigrationCosmosSettings>("target", builder.Configuration.GetSection("targetCosmosdb"))
    .AddSingleton(s =>
    {
        var legacyOptions = s.GetRequiredService<IOptionsMonitor<MigrationCosmosSettings>>().Get("legacy");
        var legacyClient = new CosmosClient(legacyOptions.Endpoint, legacyOptions.AuthKeyOrResourceToken);
        var legacyContainer = legacyClient.GetContainer(legacyOptions.DatabaseId, legacyOptions.Container);
        return new LegacyCosmosContext(legacyClient, legacyContainer);
    })
    .AddSingleton(s =>
    {
        var targetOptions = s.GetRequiredService<IOptionsMonitor<MigrationCosmosSettings>>().Get("target");
        var targetClient = new CosmosClient(targetOptions.Endpoint, targetOptions.AuthKeyOrResourceToken);
        var podcastsContainer = targetClient.GetContainer(targetOptions.DatabaseId, targetOptions.PodcastsContainer);
        var episodesContainer = targetClient.GetContainer(targetOptions.DatabaseId, targetOptions.EpisodesContainer);
        var lookupContainer = targetClient.GetContainer(targetOptions.DatabaseId, targetOptions.LookUpsContainer);
        var pushSubscriptionsContainer = targetClient.GetContainer(targetOptions.DatabaseId, targetOptions.PushSubscriptionsContainer);
        var subjectsContainer = targetClient.GetContainer(targetOptions.DatabaseId, targetOptions.SubjectsContainer);
        var discoveryContainer = targetClient.GetContainer(targetOptions.DatabaseId, targetOptions.DiscoveryContainer);
        return new TargetCosmosContext(
            targetClient,
            podcastsContainer,
            episodesContainer,
            lookupContainer,
            pushSubscriptionsContainer,
            subjectsContainer,
            discoveryContainer);
    })
    .AddSingleton<IDataRepository>(s =>
    {
        var legacyContext = s.GetRequiredService<LegacyCosmosContext>();
        var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CosmosDbRepository>>();
        return new CosmosDbRepository(legacyContext.LegacyContainer, logger);
    })
    .AddSingleton<IEpisodeMatcher, EpisodeMatcher>()
    .AddSingleton<IPodcastRepository>(s =>
    {
        var dataRepository = s.GetRequiredService<IDataRepository>();
        var episodeMatcher = s.GetRequiredService<IEpisodeMatcher>();
        var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PodcastRepository>>();
        return new PodcastRepository(dataRepository, episodeMatcher, logger);
    })
    .AddSingleton<IPodcastRepositoryV2>(s =>
    {
        var targetContext = s.GetRequiredService<TargetCosmosContext>();
        var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PodcastRepositoryV2>>();
        return new PodcastRepositoryV2(targetContext.PodcastsContainer, logger);
    })
    .AddSingleton<IEpisodeRepository>(s =>
    {
        var targetContext = s.GetRequiredService<TargetCosmosContext>();
        var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<EpisodeRepository>>();
        return new EpisodeRepository(targetContext.EpisodesContainer, logger);
    })
    .AddSingleton<ILookupRepositoryV2>(s =>
    {
        var targetContext = s.GetRequiredService<TargetCosmosContext>();
        var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LookupRepositoryV2>>();
        return new LookupRepositoryV2(targetContext.LookupContainer, logger);
    })
    .AddSingleton<IPushSubscriptionRepositoryV2>(s =>
    {
        var targetContext = s.GetRequiredService<TargetCosmosContext>();
        var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PushSubscriptionRepositoryV2>>();
        return new PushSubscriptionRepositoryV2(targetContext.PushSubscriptionsContainer, logger);
    })
    .AddSingleton<ISubjectRepositoryV2>(s =>
    {
        var targetContext = s.GetRequiredService<TargetCosmosContext>();
        var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SubjectRepositoryV2>>();
        return new SubjectRepositoryV2(targetContext.SubjectsContainer, logger);
    })
    .AddSingleton<IDiscoveryResultsRepositoryV2>(s =>
    {
        var targetContext = s.GetRequiredService<TargetCosmosContext>();
        var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DiscoveryResultsRepositoryV2>>();
        return new DiscoveryResultsRepositoryV2(targetContext.DiscoveryContainer, logger);
    })
    .AddSingleton<LegacyPodcastToV2MigrationProcessor>();

using var host = builder.Build();

var processor = host.Services.GetRequiredService<LegacyPodcastToV2MigrationProcessor>();
var result = await processor.Run();

Console.WriteLine($"Podcasts migrated: {result.PodcastsMigrated}");
Console.WriteLine($"Episodes migrated: {result.EpisodesMigrated}");
Console.WriteLine($"Failed podcasts: {result.FailedPodcastIds.Count}");
Console.WriteLine($"Failed episodes: {result.FailedEpisodeIds.Count}");
