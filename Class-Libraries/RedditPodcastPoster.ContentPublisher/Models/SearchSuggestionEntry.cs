using System.Text.Json.Serialization;

namespace RedditPodcastPoster.ContentPublisher.Models;

public record SearchSuggestionEntry(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("canonical")] string Canonical,
    [property: JsonPropertyName("searchText")] string SearchText,
    [property: JsonPropertyName("alias")] string? Alias = null);
