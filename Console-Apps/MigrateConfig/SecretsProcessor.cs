using System.Text.Json;

namespace MigrateConfig;

public class SecretsProcessor
{
    public async Task<int> Process(SecretsRequest request)
    {
        await using var file = File.OpenRead(request.Path);
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
}
