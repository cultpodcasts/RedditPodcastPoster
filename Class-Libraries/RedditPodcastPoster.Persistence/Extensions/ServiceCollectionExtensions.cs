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
                .AddSingleton<ICosmosDbContainerFactory, CosmosDbContainerFactory>()
                .AddSingleton(s => s.GetService<ICosmosDbClientFactory>()!.Create())
                .AddSingleton(s => s.GetService<ICosmosDbContainerFactory>()!.Create())
                .AddSingleton<IDataRepository, CosmosDbRepository>()
                .AddSingleton<ICosmosDbRepository, CosmosDbRepository>()
                .AddSingleton<IEpisodeMatcher, EpisodeMatcher>()
                .AddSingleton<IPodcastRepository, PodcastRepository>()
                .AddSingleton<IJsonSerializerOptionsProvider, JsonSerializerOptionsProvider>()
                .AddSingleton<IEliminationTermsRepository, EliminationTermsRepository>()
                .BindConfiguration<CosmosDbSettings>("cosmosdb");
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