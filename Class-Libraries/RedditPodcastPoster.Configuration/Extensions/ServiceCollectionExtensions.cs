using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RedditPodcastPoster.Configuration.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddPostingCriteria()
        {
            return services.BindConfiguration<PostingCriteria>("postingCriteria");
        }

        public IServiceCollection AddDelayedYouTubePublication()
        {
            return services.BindConfiguration<DelayedYouTubePublication>("delayedYouTubePublication");
        }
    }

    public static IConfigurationBuilder AddSecrets(this IConfigurationBuilder configuration, Assembly secretsAssembly)
    {
        var environment = Environment.GetEnvironmentVariable("NETCORE_ENVIRONMENT");

        var isDevelopment = string.IsNullOrEmpty(environment) || environment.ToLower() == "development";

        if (isDevelopment)
        {
            return configuration.AddUserSecrets(secretsAssembly, false);
        }

        return configuration;
    }

    extension(IServiceCollection services)
    {
        public IServiceCollection BindConfiguration<T>(string configSection)
            where T : class
        {
            services.AddOptions<T>().Configure<IConfiguration>((settings, configuration) =>
            {
                configuration.GetSection(configSection).Bind(settings);
            });
            return services;
        }

        public IServiceCollection AddDateTimeService()
        {
            return services.AddSingleton<IDateTimeService, DateTimeService>();
        }
    }
}