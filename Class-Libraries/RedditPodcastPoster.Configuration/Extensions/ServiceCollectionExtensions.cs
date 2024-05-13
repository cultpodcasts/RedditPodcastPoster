using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RedditPodcastPoster.Configuration.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPostingCriteria(this IServiceCollection services, IConfiguration config)
    {
        services.BindConfiguration<PostingCriteria>("postingCriteria");
        return services;
    }

    public static IServiceCollection AddDelayedYouTubePublication(this IServiceCollection services,
        IConfiguration config)
    {
        services.BindConfiguration<DelayedYouTubePublication>("delayedYouTubePublication");
        return services;
    }

    public static IConfigurationBuilder AddSecrets(this IConfigurationBuilder configuration, Assembly secretsAssembly)
    {
        var environment = Environment.GetEnvironmentVariable("NETCORE_ENVIRONMENT");

        var isDevelopment = string.IsNullOrEmpty(environment) ||
                            environment.ToLower() == "development";

        if (isDevelopment)
        {
            return configuration.AddUserSecrets(secretsAssembly, false);
        }

        return configuration;
    }

    public static void BindConfiguration<T>(this IServiceCollection services, IConfiguration config,
        string configSection) where T : class
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