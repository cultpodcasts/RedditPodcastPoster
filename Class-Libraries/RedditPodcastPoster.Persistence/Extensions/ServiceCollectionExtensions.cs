using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddRepositories()
        {
            return services
                .AddSingleton<ICosmosDbClientFactory, CosmosDbClientFactory>()
                .AddSingleton<ICosmosDbClientFactoryV2, CosmosDbClientFactoryV2>()
                .AddKeyedSingleton<CosmosClient>("v1", (sp, _) =>
                    sp.GetRequiredService<ICosmosDbClientFactory>().Create())
                .AddKeyedSingleton<CosmosClient>("v2", (sp, _) =>
                    sp.GetRequiredService<ICosmosDbClientFactoryV2>().Create())
                .AddSingleton<ICosmosDbContainerFactory, CosmosDbContainerFactory>()
                .AddSingleton(s => s.GetService<ICosmosDbClientFactory>()!.Create())
                .AddSingleton(s => s.GetService<ICosmosDbContainerFactory>()!.Create())
                .AddSingleton<IDataRepository, CosmosDbRepository>()
                .AddSingleton<ICosmosDbRepository, CosmosDbRepository>()
                .AddSingleton<IEpisodeMatcher, EpisodeMatcher>()
                .AddSingleton<IPodcastRepository, PodcastRepository>()
                .AddSingleton<IPodcastRepositoryV2>(s =>
                {
                    var containerFactory = s.GetRequiredService<ICosmosDbContainerFactory>();
                    var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PodcastRepositoryV2>>();
                    return new PodcastRepositoryV2(containerFactory.CreatePodcastsContainer(), logger);
                })
                .AddSingleton<IEpisodeRepository>(s =>
                {
                    var containerFactory = s.GetRequiredService<ICosmosDbContainerFactory>();
                    var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<EpisodeRepository>>();
                    return new EpisodeRepository(containerFactory.CreateEpisodesContainer(), logger);
                })
                .AddSingleton<ISubjectRepositoryV2>(s =>
                {
                    var containerFactory = s.GetRequiredService<ICosmosDbContainerFactory>();
                    var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SubjectRepositoryV2>>();
                    return new SubjectRepositoryV2(containerFactory.CreateSubjectsContainer(), logger);
                })
                .AddSingleton<IDiscoveryResultsRepositoryV2>(s =>
                {
                    var containerFactory = s.GetRequiredService<ICosmosDbContainerFactory>();
                    var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DiscoveryResultsRepositoryV2>>();
                    return new DiscoveryResultsRepositoryV2(containerFactory.CreateDiscoveryContainer(), logger);
                })
                .AddSingleton<IActivityRepositoryV2>(s =>
                {
                    var containerFactory = s.GetRequiredService<ICosmosDbContainerFactory>();
                    var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ActivityRepositoryV2>>();
                    return new ActivityRepositoryV2(containerFactory.CreateActivitiesContainer(), logger);
                })
                .AddSingleton<ILookupRepositoryV2>(s =>
                {
                    var containerFactory = s.GetRequiredService<ICosmosDbContainerFactory>();
                    var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LookupRepositoryV2>>();
                    return new LookupRepositoryV2(containerFactory.CreateLookUpsContainer(), logger);
                })
                .AddSingleton<IPushSubscriptionRepositoryV2>(s =>
                {
                    var containerFactory = s.GetRequiredService<ICosmosDbContainerFactory>();
                    var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PushSubscriptionRepositoryV2>>();
                    return new PushSubscriptionRepositoryV2(containerFactory.CreatePushSubscriptionsContainer(), logger);
                })
                .AddSingleton<IJsonSerializerOptionsProvider, JsonSerializerOptionsProvider>()
                .AddSingleton<IEliminationTermsRepository, EliminationTermsRepository>()
                .BindConfiguration<CosmosDbSettings>("cosmosdb")
                .BindConfiguration<CosmosDbSettingsV2>("cosmosdbv2");
        }

        public IServiceCollection AddFileRepository(string containerName = "",
            bool useEntityFolder = false)
        {
            return services
                .AddSingleton<IJsonSerializerOptionsProvider, JsonSerializerOptionsProvider>()
                .AddScoped<IFileRepositoryFactory, FileRepositoryFactory>()
                .AddScoped(x => x.GetService<IFileRepositoryFactory>()!.Create(containerName, useEntityFolder));
        }

        public IServiceCollection AddSafeFileWriter()
        {
            return services
                .AddScoped<ISafeFileEntityWriter, SafeFileEntityWriter>();
        }
    }
}