using System.Text.Json;
using System.Text.Json.Nodes;
using CommandLine;
using LaunchSettingsToAppSettings;

return await Parser.Default.ParseArguments<Request>(args)
    .MapResult(async request => await Run(request), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(Request request)
{
    await using var file = File.OpenRead(request.Path);
    var launchSettings = await JsonSerializer.DeserializeAsync<Dictionary<string, JsonNode>>(file);
    var profile = launchSettings["profiles"];
    var namedProfile = profile[request.Profile];
    var environmentVariablesJson = namedProfile["environmentVariables"].ToJsonString();
    var appSettings = new List<AppSetting>();
    var environmentVariablesDict = JsonSerializer.Deserialize<Dictionary<string, string>>(environmentVariablesJson);

    foreach (var key in environmentVariablesDict.Keys)
    {
        var item = environmentVariablesDict[key];
        var appSetting = new AppSetting(key, item, false);
        appSettings.Add(appSetting);
    }

    var json = JsonSerializer.Serialize(appSettings, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    });
    Console.WriteLine(json);

    return 0;
}

internal record AppSetting(string Name, string Value, bool SlotSetting);