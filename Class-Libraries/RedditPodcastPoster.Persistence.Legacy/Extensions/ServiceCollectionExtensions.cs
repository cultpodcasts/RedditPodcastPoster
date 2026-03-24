using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence.Legacy.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers V1 (legacy) Cosmos DB client, container, and repositories.
        /// Only call this from apps that need access to the old single-container database.
        /// </summary>
        public IServiceCollection AddLegacyCosmosDb()
        {
            return services
                .AddSingleton<ICosmosDbClientFactory, CosmosDbClientFactory>()
                .AddKeyedSingleton<CosmosClient>("v1", (sp, _) =>
                    sp.GetRequiredService<ICosmosDbClientFactory>().Create())
                .AddSingleton(s => s.GetService<ICosmosDbClientFactory>()!.Create())
                .AddKeyedSingleton<Container>("v1", (sp, _) =>
                {
                    var settings =
                        sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CosmosDbSettings>>().Value;
                    var client = sp.GetRequiredService<ICosmosDbClientFactory>().Create();
                    return client.GetContainer(settings.DatabaseId, settings.Container);
                })
                .AddSingleton<IDataRepository, CosmosDbRepository>()
                .AddSingleton<ICosmosDbRepository, CosmosDbRepository>()
                .BindConfiguration<CosmosDbSettings>("cosmosdb");
        }

        /// <summary>
        /// Registers the legacy single-container podcast repository.
        /// Only call this from apps that need the old embedded-episode model.
        /// </summary>
        public IServiceCollection AddLegacyPodcastRepository()
        {
            return services
                .AddSingleton<IPodcastRepository, PodcastRepository>();
        }

        /// <summary>
        /// Registers the legacy subject repository (single-container, pre-V2).
        /// Only call this from apps that need the old subject data access.
        /// </summary>
        public IServiceCollection AddLegacySubjectRepository()
        {
            return services
                .AddSingleton<ISubjectRepository, SubjectRepository>();
        }

        /// <summary>
        /// Registers the legacy push-subscription repository (single-container, pre-V2).
        /// Only call this from apps that need the old push-subscription data access.
        /// </summary>
        public IServiceCollection AddLegacyPushSubscriptionRepository()
        {
            return services
                .AddSingleton<IPushSubscriptionRepository, PushSubscriptionRepository>();
        }

        /// <summary>
        /// Registers the legacy discovery-results repository (single-container, pre-V2).
        /// Only call this from apps that need the old discovery data access.
        /// </summary>
        public IServiceCollection AddLegacyDiscoveryResultsRepository()
        {
            return services
                .AddSingleton<IDiscoveryResultsRepository, DiscoveryResultsRepository>();
        }
    }
}
