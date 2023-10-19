﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RedditPodcastPoster.Persistence.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRepositories(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<CosmosDbSettings>().Bind(config.GetSection("cosmosdb"));

        CosmosDbClientFactory.AddCosmosClient(services);

        return services.AddScoped<IDataRepository, CosmosDbRepository>()
            .AddScoped<ICosmosDbRepository, CosmosDbRepository>()
            .AddScoped<IEpisodeMatcher, EpisodeMatcher>()
            .AddScoped<IPodcastRepository, PodcastRepository>()
            .AddSingleton<IJsonSerializerOptionsProvider, JsonSerializerOptionsProvider>()
            .AddScoped<IEliminationTermsRepository, EliminationTermsRepository>();
    }

    public static IServiceCollection AddFileRepository(this IServiceCollection services)
    {
        return services
            .AddScoped<IFileRepositoryFactory, FileRepositoryFactory>()
            .AddScoped(services => services.GetService<IFileRepositoryFactory>()!.Create());
    }
}