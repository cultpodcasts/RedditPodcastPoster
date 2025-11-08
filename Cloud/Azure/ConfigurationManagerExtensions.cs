using Microsoft.Extensions.Configuration;

namespace Azure;

public static class ConfigurationManagerExtensions
{
    public static bool IsDevelopment(this IConfigurationManager configurationManager)
    {
        var environment = configurationManager["DOTNET_environment"];
        return environment?.ToLowerInvariant() == "development";
    }
}