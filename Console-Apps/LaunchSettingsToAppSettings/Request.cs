using CommandLine;

namespace LaunchSettingsToAppSettings;

internal class Request
{
    [Value(0, MetaName = "launch-settings-location", HelpText = "Location of the launch settings file", Required = true)]
    public required string Path { get; set; }

    [Value(1, MetaName = "profile name", HelpText = "Name of the profile in the launch settings file", Required = true)]
    public required string Profile { get; set; }

}