using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MigrateConfig;

[JsonSerializable(typeof(Dictionary<string, JsonNode>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(List<AppSetting>))]
[JsonSerializable(typeof(AppSetting))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
internal partial class ConfigJsonContext : JsonSerializerContext;

internal record AppSetting(string Name, string Value, bool SlotSetting);
