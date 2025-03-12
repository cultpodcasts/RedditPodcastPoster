using Microsoft.Extensions.Configuration;

namespace Azure;

public static class ConfigurationManagerExtensions
{
    private const string UseApplicationInsightsConfigKey = "_UseApplicationInsightsConfig";

    public static bool IsDevelopment(this IConfigurationManager configurationManager)
    {
        var environment = configurationManager["DOTNET_environment"];
        return environment?.ToLowerInvariant() != "development";
    }
}