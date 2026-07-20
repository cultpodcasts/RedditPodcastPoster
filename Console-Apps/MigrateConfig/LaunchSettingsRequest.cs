namespace MigrateConfig;

/// <summary>Command args for former LaunchSettingsToAppSettings.</summary>
public class LaunchSettingsRequest
{
    public required string Path { get; init; }

    public required string Profile { get; init; }
}
