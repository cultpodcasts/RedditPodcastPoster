using System.Text.Json;
using System.Text.Json.Serialization;

namespace EpisodeServiceBackfill;

/// <summary>
/// STJ options for leftover-document / catalog-patch deserialize in this CLI.
/// Persistence Cosmos options stay on <c>JsonSerializerOptionsProvider</c> (adds RegexConverter).
/// </summary>
public static class EpisodeDocumentJsonOptions
{
    public static JsonSerializerOptions Instance { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };
}
