using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRepositories(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddScoped<ICosmosDbClientFactory, CosmosDbClientFactory>()
            .AddScoped<ICosmosDbContainerFactory, CosmosDbContainerFactory>()
            .AddScoped(s => s.GetService<ICosmosDbClientFactory>()!.Create())
            .AddScoped(s => s.GetService<ICosmosDbContainerFactory>()!.Create())
            .AddScoped<IDataRepository, CosmosDbRepository>()
            .AddScoped<ICosmosDbRepository, CosmosDbRepository>()
            .AddScoped<IEpisodeMatcher, EpisodeMatcher>()
            .AddScoped<IPodcastRepository, PodcastRepository>()
            .AddSingleton<IJsonSerializerOptionsProvider, JsonSerializerOptionsProvider>()
            .AddScoped<IEliminationTermsRepository, EliminationTermsRepository>();

        services.Configure<CosmosDbSettings>(settings => config.GetSection("cosmosdb").Bind(settings));

        return services;
    }

    public static IServiceCollection AddFileRepository(this IServiceCollection services, string containerName = "")
    {
        return services
            .AddScoped<IFileRepositoryFactory, FileRepositoryFactory>()
            .AddScoped(services => services.GetService<IFileRepositoryFactory>()!.Create(containerName));
    }
}