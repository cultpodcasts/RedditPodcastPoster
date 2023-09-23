using Microsoft.Extensions.Configuration;

namespace Azure;

public static class ConfigurationBuilderExtensions
{
    public static IConfiguration AddToConfigurationBuilder<T>(this IConfigurationBuilder configurationBuilder) where T : class
    {
        return
            configurationBuilder
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddUserSecrets<T>()
                .AddEnvironmentVariables()
                .Build();
    }
}