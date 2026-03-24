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
    }
}
