using System.Text.Json.Serialization;
using Api.Contracts;

namespace Api.Dtos;

/// <summary>
/// Public episode JSON returned by <c>/public/episode/{id}</c> (website bookmarks). Exported to
/// <c>@cultpodcasts/contracts</c> (RPP #1502); <see cref="Ids"/> and <see cref="Services"/> use the
/// JSON-identical boundary DTOs so the contract stays inside <c>Api.Dtos</c>.
/// </summary>
[ExportContract]
public class PublicEpisodeDto : IPlayableKindDto
{
    /// <summary><c>Episode</c>, <c>NewsReport</c>, <c>TvShowEpisode</c> or <c>Film</c>.</summary>
    [JsonPropertyName("contentKind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(300)]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ContentKindDto? ContentKind { get; set; }

    /// <summary>Podcast, news organisation or TV show id; null for a film.</summary>
    [JsonPropertyName("parentId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(301)]
    public Guid? ParentId { get; set; }

    /// <summary>Podcast, news organisation or TV show name; null for a film.</summary>
    [JsonPropertyName("parentName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(302)]
    public string? ParentName { get; set; }

    /// <summary>Site route segment: the parent name, or a film's own name.</summary>
    [JsonPropertyName("slug")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(303)]
    public string? Slug { get; set; }

    /// <summary>Site path with the kind's short id, e.g. <c>/news/{slug}/{shortId}</c>.</summary>
    [JsonPropertyName("path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(304)]
    public string? Path { get; set; }

    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public Guid Id { get; set; }

    [JsonPropertyName("podcastName")]
    [JsonPropertyOrder(30)]
    public string PodcastName { get; set; } = "";

    [JsonPropertyName("title")]
    [JsonPropertyOrder(31)]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    [JsonPropertyOrder(40)]
    public string Description { get; set; } = "";

    [JsonPropertyName("release")]
    [JsonPropertyOrder(70)]
    public DateTime Release { get; set; }

    [JsonPropertyName("duration")]
    [JsonPropertyOrder(71)]
    public TimeSpan Length { get; set; }

    [JsonPropertyName("explicit")]
    [JsonPropertyOrder(72)]
    public bool Explicit { get; set; }

    [JsonPropertyName("subjects")]
    [JsonPropertyOrder(90)]
    public List<string> Subjects { get; set; } = [];

    [JsonPropertyName("ids")]
    [JsonPropertyOrder(100)]
    public EpisodeIdsDto? Ids { get; set; }

    [JsonPropertyName("services")]
    [JsonPropertyOrder(101)]
    public Dictionary<string, ServiceLinkDto>? Services { get; set; }

    [JsonPropertyName("image")]
    [JsonPropertyOrder(250)]
    public Uri? Image { get; set; }

    /// <summary>
    /// RPP #1500: true when a curator removed this episode's image (<see cref="Image"/> is then null); clients show no
    /// art instead of the YouTube thumbnail. Omitted otherwise.
    /// </summary>
    [JsonPropertyName("imageRemoved")]
    [JsonPropertyOrder(252)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ImageRemoved { get; set; }

    /// <summary>Other titles a film is known by (RPP #1431). Omitted for other kinds.</summary>
    [JsonPropertyName("alternativeTitles")]
    [JsonPropertyOrder(251)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? AlternativeTitles { get; set; }
}
