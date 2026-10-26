using System.Text.Json;
using System.Text.Json.Serialization;

namespace CataloguePlayableServicesBackfill;

public static class BackupDocumentJsonOptions
{
    public static JsonSerializerOptions Instance { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };
}
