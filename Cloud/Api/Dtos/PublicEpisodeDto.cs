using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;

namespace Api.Dtos;

public class PublicEpisodeDto : IPlayableKindDto
{
    /// <summary><c>Episode</c>, <c>NewsReport</c>, <c>TvShowEpisode</c> or <c>Film</c>.</summary>
    [JsonPropertyName("contentKind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(300)]
    public string? ContentKind { get; set; }

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
    public EpisodeIds? Ids { get; set; }

    [JsonPropertyName("services")]
    [JsonPropertyOrder(101)]
    public Dictionary<string, ServiceLink>? Services { get; set; }

    [JsonPropertyName("image")]
    [JsonPropertyOrder(250)]
    public Uri? Image { get; set; }
}
