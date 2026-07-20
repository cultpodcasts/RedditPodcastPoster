using System.Text.Json;
using System.Text.Json.Nodes;
using MigrateConfig;

if (args.Any(IsHelpArg) || args.Length == 0)
{
    PrintUsage();
    return args.Length == 0 ? 1 : 0;
}

var mode = args[0].ToLowerInvariant();
return mode switch
{
    "secrets" or "s" => await ConvertSecrets(args),
    "launch-settings" or "launchsettings" or "ls" => await ConvertLaunchSettings(args),
    _ => UnknownMode(args[0])
};

static async Task<int> ConvertSecrets(string[] args)
{
    if (args.Length != 2)
    {
        Console.Error.WriteLine("Usage: MigrateConfig secrets <secrets-json-path>");
        return 1;
    }

    await using var file = File.OpenRead(args[1]);
    var secrets = await JsonSerializer.DeserializeAsync(file, ConfigJsonContext.Default.DictionaryStringJsonNode);
    if (secrets is null)
    {
        return 1;
    }

    var appSettings = new List<AppSetting>();
    foreach (var (key, item) in secrets)
    {
        var itemValue = item.ToString();
        var appSettingName = key.Replace(":", "__");
        appSettings.Add(new AppSetting(appSettingName, itemValue, false));
    }

    var json = JsonSerializer.Serialize(appSettings, ConfigJsonContext.Default.ListAppSetting);
    Console.WriteLine(json);
    return 0;
}

static async Task<int> ConvertLaunchSettings(string[] args)
{
    if (args.Length != 3)
    {
        Console.Error.WriteLine("Usage: MigrateConfig launch-settings <launch-settings-path> <profile-name>");
        return 1;
    }

    await using var file = File.OpenRead(args[1]);
    var launchSettings = await JsonSerializer.DeserializeAsync(file, ConfigJsonContext.Default.DictionaryStringJsonNode);
    if (launchSettings is null)
    {
        return 1;
    }

    if (!launchSettings.TryGetValue("profiles", out var profilesNode) || profilesNode is not JsonObject profiles)
    {
        Console.Error.WriteLine("launchSettings.json has no 'profiles' object.");
        return 1;
    }

    var profileName = args[2];
    if (!profiles.TryGetPropertyValue(profileName, out var namedProfile) || namedProfile is null)
    {
        Console.Error.WriteLine($"Profile '{profileName}' not found.");
        return 1;
    }

    var environmentVariablesJson = namedProfile["environmentVariables"]?.ToJsonString();
    if (string.IsNullOrWhiteSpace(environmentVariablesJson))
    {
        Console.Error.WriteLine($"Profile '{profileName}' has no environmentVariables.");
        return 1;
    }

    var environmentVariablesDict =
        JsonSerializer.Deserialize(environmentVariablesJson, ConfigJsonContext.Default.DictionaryStringString);
    if (environmentVariablesDict is null)
    {
        return 1;
    }

    var appSettings = environmentVariablesDict
        .Select(kv => new AppSetting(kv.Key, kv.Value, false))
        .ToList();

    var json = JsonSerializer.Serialize(appSettings, ConfigJsonContext.Default.ListAppSetting);
    Console.WriteLine(json);
    return 0;
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
          secrets           User-secrets JSON (section:key) → section__key app settings
          launch-settings   launchSettings.json profile environmentVariables → app settings

        Examples:
          MigrateConfig secrets path-to-secrets.json
          MigrateConfig launch-settings Cloud/Indexer/Properties/launchSettings.json Indexer
        """);
}
