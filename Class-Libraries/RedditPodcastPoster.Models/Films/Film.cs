using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Services;

namespace RedditPodcastPoster.Models.Films;

/// <summary>
/// Standalone made-as-film playable (no parent). ADR-0002 / epic S-001…S-002.
/// Platform presence is <see cref="Services"/> only — no provider-id fields
/// (unlike podcast <c>Episode</c>, which tracks Spotify/Apple/YouTube collection identity).
/// Release is year or calendar date — not a podcast-episode datetime.
/// </summary>
[CosmosSelector(ModelType.Film)]
public sealed class Film : CosmosSelector
{
    public Film()
    {
        Id = Guid.NewGuid();
        ModelType = ModelType.Film;
    }

    public Film(string title) : this()
    {
        Title = title;
        FileKey = FileKeyFactory.GetFileKey(title);
    }

    [JsonPropertyName("title")]
    [JsonPropertyOrder(10)]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonPropertyOrder(20)]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("release")]
    [JsonPropertyOrder(30)]
    public CatalogueRelease? Release { get; set; }

    [JsonPropertyName("duration")]
    [JsonPropertyOrder(31)]
    public TimeSpan Length { get; set; }

    [JsonPropertyName("explicit")]
    [JsonPropertyOrder(32)]
    public bool Explicit { get; set; }

    [JsonPropertyName("lang")]
    [JsonPropertyOrder(45)]
    public string? Language { get; set; }

    [JsonPropertyName("ignored")]
    [JsonPropertyOrder(43)]
    public bool Ignored { get; set; }

    [JsonPropertyName("removed")]
    [JsonPropertyOrder(44)]
    public bool Removed { get; set; }

    [JsonPropertyName("subjects")]
    [JsonPropertyOrder(70)]
    public List<string> Subjects { get; set; } = [];

    [JsonPropertyName("removedSubjects")]
    [JsonPropertyOrder(71)]
    public List<string> RemovedSubjects { get; set; } = [];

    [JsonPropertyName("searchTerms")]
    [JsonPropertyOrder(80)]
    public string? SearchTerms { get; set; }

    [JsonPropertyName("services")]
    [JsonPropertyOrder(151)]
    public Dictionary<string, ServiceLink>? Services { get; set; }
}
