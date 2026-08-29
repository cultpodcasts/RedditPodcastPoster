using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.Serialization;

/// <summary>
/// Shared STJ options for episode document / catalog-patch deserialize (camelCase + enums).
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
