using Microsoft.Extensions.Configuration;

namespace Azure;

public static class ConfigurationManagerExtensions
{
    public static void AddLocalConfiguration<T>(this IConfigurationManager configurationManager) where T : class {
        var configurationBuilder = new ConfigurationBuilder();;
        var config= configurationBuilder
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddUserSecrets<T>()
            .AddEnvironmentVariables()
            .Build();
        configurationManager.AddJsonFile("local.settings.json", false);
        configurationManager.AddConfiguration(config);
    }
}