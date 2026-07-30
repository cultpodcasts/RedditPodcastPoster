using System.Text.Json.Serialization;

namespace RedditPodcastPoster.ContentPublisher.Models;

public record SearchSuggestionsCorpus(
    [property: JsonPropertyName("generatedAtUtc")] DateTime GeneratedAtUtc,
    [property: JsonPropertyName("entries")] SearchSuggestionEntry[] Entries);
