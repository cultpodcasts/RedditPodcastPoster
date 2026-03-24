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
                .AddSingleton<ICosmosDbClientFactoryV2, CosmosDbClientFactoryV2>()
                .AddKeyedSingleton<CosmosClient>("v2", (sp, _) =>
                    sp.GetRequiredService<ICosmosDbClientFactoryV2>().Create())
                .AddSingleton<ICosmosDbContainerFactory, CosmosDbContainerFactory>()
                .AddSingleton<IEpisodeMatcher, EpisodeMatcher>()
                .AddSingleton<IEpisodeMerger, EpisodeMerger>()
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
                .AddSingleton<IActivityRepository>(s =>
                {
                    var containerFactory = s.GetRequiredService<ICosmosDbContainerFactory>();
                    var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ActivityRepository>>();
                    return new ActivityRepository(containerFactory.CreateActivitiesContainer(), logger);
                })
                .AddSingleton<ILookupRepositoryV2>(s =>
                {
                    var containerFactory = s.GetRequiredService<ICosmosDbContainerFactory>();
                    var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LookupRepositoryV2>>();
                    return new LookupRepositoryV2(containerFactory.CreateLookUpsContainer(), logger);
                })
                .AddSingleton<IJsonSerializerOptionsProvider, JsonSerializerOptionsProvider>()
                .AddSingleton<IEliminationTermsRepository, EliminationTermsRepository>()
                .BindConfiguration<CosmosDbSettingsV2>("cosmosdbv2");
        }

        public IServiceCollection AddLegacyPodcastRepository()
        {
            return services
                .AddSingleton<IPodcastRepository, PodcastRepository>();
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