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
            .AddScoped<IEliminationTermsRepository, EliminationTermsRepository>()
//            .AddOptions<CosmosDbSettings>().Bind(config.GetSection("cosmosdb"))
            ;
        services.BindConfiguration<CosmosDbSettings>("cosmosdb");
        return services;
    }

    public static IServiceCollection AddFileRepository(this IServiceCollection services, string containerName = "")
    {
        return services
            .AddScoped<IFileRepositoryFactory, FileRepositoryFactory>()
            .AddScoped(services => services.GetService<IFileRepositoryFactory>()!.Create(containerName));
    }
}

public static class ConfigExtensions
{
    public static void BindConfiguration<T>(this IServiceCollection services, IConfiguration config, string configSection) where T : class
    {
        services.AddOptions<T>().Bind(config.GetSection(configSection));
    }
    public static void BindConfiguration<T>(this IServiceCollection services, string configSection) where T : class
    {
        services.AddOptions<T>().Configure<IConfiguration>((settings, configuration) =>

        {

            configuration.GetSection(configSection).Bind(settings);

        });
    }
}