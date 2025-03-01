using Microsoft.Extensions.Configuration;

namespace Azure;

public static class ConfigurationManagerExtensions
{
    private const string UseApplicationInsightsConfigKey = "_UseApplicationInsightsConfig";

    public static bool UseApplicationInsightsConfiguration(this IConfigurationManager configurationManager)
    {
        var appInsightsSetting = configurationManager[UseApplicationInsightsConfigKey];
        if (bool.TryParse(appInsightsSetting ?? false.ToString(), out var settingValue))
        {
            return settingValue;
        }

        throw new ArgumentException(
            $"Unable to parse as bool configuration with key'{UseApplicationInsightsConfigKey}' has value '{configurationManager[UseApplicationInsightsConfigKey]}'.");
    }
}