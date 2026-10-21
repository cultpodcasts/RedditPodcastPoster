using System.Text.Json.Serialization;

namespace RedditPodcastPoster.ContentPublisher.Models;

/// <summary>
/// One row of the flat typeahead index. <see cref="Type"/> is <c>subject</c>, <c>podcast</c>,
/// <c>tvShow</c>, <c>newsOrganisation</c> or <c>film</c>. <see cref="Id"/> is set on film rows only
/// (film names can repeat, so the website links <c>/film/{name}/{shortId}</c> from it) and is
/// omitted from JSON when null.
/// </summary>
public record SearchSuggestionEntry(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("canonical")] string Canonical,
    [property: JsonPropertyName("searchText")] string SearchText,
    [property: JsonPropertyName("alias")] string? Alias = null,
    [property: JsonPropertyName("id")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Id = null);
