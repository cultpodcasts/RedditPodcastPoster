using System.Text.Json;
using System.Text.Json.Nodes;
using SecretsToFunctionSettings;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: SecretsToFunctionSettings <secrets-json-path>");
    return 1;
}

return await Run(args[0]);

async Task<int> Run(string path)
{
    await using var file = File.OpenRead(path);
    var secrets = await JsonSerializer.DeserializeAsync(file, SecretsJsonContext.Default.DictionaryStringJsonNode);
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

    var json = JsonSerializer.Serialize(appSettings, SecretsJsonContext.Default.ListAppSetting);
    Console.WriteLine(json);
    return 0;
}

internal record AppSetting(string Name, string Value, bool SlotSetting);
