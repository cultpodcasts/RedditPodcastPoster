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
        return new TargetCosmosContext(targetClient, podcastsContainer, episodesContainer);
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
    .AddSingleton<LegacyPodcastToV2MigrationProcessor>();

using var host = builder.Build();

var processor = host.Services.GetRequiredService<LegacyPodcastToV2MigrationProcessor>();
var result = await processor.Run();

Console.WriteLine($"Podcasts migrated: {result.PodcastsMigrated}");
Console.WriteLine($"Episodes migrated: {result.EpisodesMigrated}");
Console.WriteLine($"Failed podcasts: {result.FailedPodcastIds.Count}");
Console.WriteLine($"Failed episodes: {result.FailedEpisodeIds.Count}");
