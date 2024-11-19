using System.Text.Json;
using System.Text.Json.Nodes;
using CommandLine;
using SecretsToFunctionSettings;

return await Parser.Default.ParseArguments<Request>(args)
    .MapResult(async request => await Run(request), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(Request request)
{
    await using var file = File.OpenRead(request.Path);
    var secrets = await JsonSerializer.DeserializeAsync<Dictionary<string, JsonNode>>(file);
    var appSettings = new List<AppSetting>();
    foreach (var key in secrets.Keys)
    {
        var item = secrets[key];
        var itemValue = item.ToString();
        var appSettingName = key.Replace(":", "__");
        var appSetting = new AppSetting(appSettingName, itemValue, false);
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