using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace RedditPodcastPoster.Configuration.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPostingCriteria(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<PostingCriteria>().Bind(config.GetSection("postingCriteria"));
        return services;
    }
    public static IServiceCollection AddDelayedYouTubePublication(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<DelayedYouTubePublication>().Bind(config.GetSection("delayedYouTubePublication"));
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

}
