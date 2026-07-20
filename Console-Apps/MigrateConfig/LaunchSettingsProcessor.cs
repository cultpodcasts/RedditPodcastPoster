using System.Text.Json;
using System.Text.Json.Nodes;

namespace MigrateConfig;

public class LaunchSettingsProcessor
{
    public async Task<int> Process(LaunchSettingsRequest request)
    {
        await using var file = File.OpenRead(request.Path);
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

        if (!profiles.TryGetPropertyValue(request.Profile, out var namedProfile) || namedProfile is null)
        {
            Console.Error.WriteLine($"Profile '{request.Profile}' not found.");
            return 1;
        }

        var environmentVariablesJson = namedProfile["environmentVariables"]?.ToJsonString();
        if (string.IsNullOrWhiteSpace(environmentVariablesJson))
        {
            Console.Error.WriteLine($"Profile '{request.Profile}' has no environmentVariables.");
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
}
