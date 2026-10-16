using CommandLine;

namespace MigrateConfig;

[Verb("launch-settings", HelpText = "Convert a launchSettings profile environment to Azure function app-setting JSON.")]
public class LaunchSettingsRequest
{
    [Value(0, MetaName = "launch-settings-path", Required = true, HelpText = "Path to launchSettings.json.")]
    public string Path { get; set; } = "";

    [Value(1, MetaName = "profile-name", Required = true, HelpText = "Profile name within launchSettings.json.")]
    public string Profile { get; set; } = "";
}
