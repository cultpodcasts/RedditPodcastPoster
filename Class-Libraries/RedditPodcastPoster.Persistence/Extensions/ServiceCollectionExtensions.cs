using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services
            .AddSingleton<ICosmosDbClientFactory, CosmosDbClientFactory>()
            .AddSingleton<ICosmosDbContainerFactory, CosmosDbContainerFactory>()
            .AddSingleton(s => s.GetService<ICosmosDbClientFactory>()!.Create())
            .AddSingleton(s => s.GetService<ICosmosDbContainerFactory>()!.Create())
            .AddSingleton<IDataRepository, CosmosDbRepository>()
            .AddSingleton<ICosmosDbRepository, CosmosDbRepository>()
            .AddSingleton<IEpisodeMatcher, EpisodeMatcher>()
            .AddSingleton<IPodcastRepository, PodcastRepository>()
            .AddSingleton<IJsonSerializerOptionsProvider, JsonSerializerOptionsProvider>()
            .AddSingleton<IEliminationTermsRepository, EliminationTermsRepository>();
        services.BindConfiguration<CosmosDbSettings>("cosmosdb");
        return services;
    }

    public static IServiceCollection AddFileRepository(this IServiceCollection services, string containerName = "")
    {
        return services
            .AddScoped<IFileRepositoryFactory, FileRepositoryFactory>()
            .AddScoped(x => x.GetService<IFileRepositoryFactory>()!.Create(containerName));
    }
}