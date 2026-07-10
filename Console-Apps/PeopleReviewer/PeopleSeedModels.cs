using System.Text.Json.Serialization;

namespace PeopleReviewer;

internal sealed class PeopleSeedDocument
{
    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; set; }

    [JsonPropertyName("sourceCache")]
    public string? SourceCache { get; set; }

    [JsonPropertyName("sourceBackupPath")]
    public string? SourceBackupPath { get; set; }

    [JsonPropertyName("people")]
    public List<PeopleSeedEntry> People { get; set; } = [];
}

internal sealed class PeopleSeedEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional curator override for surname-style ordering (mirrors Person.sortName).
    /// </summary>
    [JsonPropertyName("sortName")]
    public string? SortName { get; set; }

    [JsonPropertyName("aliases")]
    public string[] Aliases { get; set; } = [];

    [JsonPropertyName("twitterHandle")]
    public string? TwitterHandle { get; set; }

    [JsonPropertyName("blueskyHandle")]
    public string? BlueskyHandle { get; set; }

    [JsonPropertyName("sourceEpisodeIds")]
    public List<string> SourceEpisodeIds { get; set; } = [];

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}
