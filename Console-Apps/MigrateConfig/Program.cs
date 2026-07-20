using MigrateConfig;

if (args.Any(IsHelpArg) || args.Length == 0)
{
    PrintUsage();
    return args.Length == 0 ? 1 : 0;
}

var mode = args[0].ToLowerInvariant();
return mode switch
{
    "secrets" or "s" => await RunSecrets(args),
    "launch-settings" or "launchsettings" or "ls" => await RunLaunchSettings(args),
    _ => UnknownMode(args[0])
};

static async Task<int> RunSecrets(string[] args)
{
    if (args.Length != 2)
    {
        Console.Error.WriteLine("Usage: MigrateConfig secrets <secrets-json-path>");
        return 1;
    }

    return await new SecretsProcessor().Process(new SecretsRequest { Path = args[1] });
}

static async Task<int> RunLaunchSettings(string[] args)
{
    if (args.Length != 3)
    {
        Console.Error.WriteLine("Usage: MigrateConfig launch-settings <launch-settings-path> <profile-name>");
        return 1;
    }

    return await new LaunchSettingsProcessor().Process(new LaunchSettingsRequest
    {
        Path = args[1],
        Profile = args[2]
    });
}

static int UnknownMode(string mode)
{
    Console.Error.WriteLine($"Unknown mode: {mode}");
    PrintUsage();
    return 1;
}

static bool IsHelpArg(string arg) =>
    arg is "--help" or "-h" or "-?" or "help";

static void PrintUsage()
{
    Console.WriteLine("""
        MigrateConfig — convert local config JSON to Azure function app-setting JSON.

        Usage:
          MigrateConfig secrets <secrets-json-path>
          MigrateConfig launch-settings <launch-settings-path> <profile-name>

        Modes:
          secrets           SecretsProcessor — user-secrets JSON → app settings
          launch-settings   LaunchSettingsProcessor — launchSettings profile env → app settings

        Examples:
          MigrateConfig secrets path-to-secrets.json
          MigrateConfig launch-settings Cloud/Indexer/Properties/launchSettings.json Indexer
        """);
}
