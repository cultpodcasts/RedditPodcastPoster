using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace RedditPodcastPoster.Common;

public static class ConfigurationExtensions
{
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