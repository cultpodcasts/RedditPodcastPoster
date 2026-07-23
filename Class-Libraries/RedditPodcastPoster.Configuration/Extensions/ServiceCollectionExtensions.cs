using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Configuration.Services;
using RedditPodcastPoster.Configuration.Validators;

namespace RedditPodcastPoster.Configuration.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddPostingCriteria()
        {
            services
                .BindConfiguration<PostingCriteria>("postingCriteria")
                .AddSingleton<IValidateOptions<PostingCriteria>, PostingCriteriaValidator>();
            services.AddOptions<PostingCriteria>().ValidateOnStart();
            return services;
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
