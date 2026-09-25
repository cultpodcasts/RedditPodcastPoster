using System.Text.Json.Serialization;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

namespace RedditPodcastPoster.Search.Models;

public class EpisodeSearchRecord
{
    [SimpleField(IsKey = true, IsFilterable = true, IsSortable = false, IsFacetable = false)]
    public required string Id { get; set; }

    /// <summary>
    /// Playable kind: Episode, TvShowEpisode, Film, or NewsReport.
    /// Omitted filter means all kinds.
    /// Null is omitted from the hourly upload until the live index has the field.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [SimpleField(IsFilterable = true, IsFacetable = true, IsSortable = false)]
    public string? ContentKind { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [SearchableField(AnalyzerName = LexicalAnalyzerName.Values.EnLucene, IsFilterable = false, IsFacetable = false,
        IsSortable = false)]
    public string? Title { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [SearchableField(IsFilterable = true, IsFacetable = true, AnalyzerName = LexicalAnalyzerName.Values.EnLucene,
        IsSortable = false)]
    public string? SeriesName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [SearchableField(AnalyzerName = LexicalAnalyzerName.Values.EnLucene, IsFilterable = false, IsSortable = false,
        IsFacetable = false)]
    public string? Description { get; set; }

    /// <summary>
    /// Legacy names for the index that is live today. A rebuilt index does not include them:
    /// <c>title</c>, <c>seriesName</c>, and <c>description</c> replace them. Null is omitted
    /// from the upload so the new index is not sent both shapes.
    /// </summary>
    [FieldBuilderIgnore]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EpisodeTitle { get; set; }

    [FieldBuilderIgnore]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PodcastName { get; set; }

    [FieldBuilderIgnore]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EpisodeDescription { get; set; }

    [SimpleField(IsSortable = true, IsFacetable = false, IsFilterable = false)]
    public required DateTimeOffset Release { get; set; }

    [SimpleField(IsSortable = false, IsFacetable = false, IsFilterable = false)]
    public required string Duration { get; set; }

    [SimpleField(IsSortable = false, IsFilterable = false, IsFacetable = false)]
    public string? SpotifyId { get; set; }

    [SimpleField(IsSortable = false, IsFilterable = false, IsFacetable = false)]
    public string? AppleId { get; set; }

    [SimpleField(IsSortable = false, IsFilterable = false, IsFacetable = false)]
    public string? PodcastAppleId { get; set; }

    [SimpleField(IsSortable = false, IsFilterable = false, IsFacetable = false)]
    public string? YoutubeId { get; set; }

    [SimpleField(IsSortable = false, IsFilterable = false, IsFacetable = false)]
    public required string BBC { get; set; }

    [SimpleField(IsSortable = false, IsFilterable = false, IsFacetable = false)]
    public required string InternetArchive { get; set; }

    /// <summary>
    /// Compact service URLs that are not reconstructed from Spotify/Apple/YouTube ids
    /// (<c>key:payload|...</c>). Empty string when none — never null (Azure Search merge ignores null).
    /// </summary>
    [SimpleField(IsSortable = false, IsFilterable = false, IsFacetable = false)]
    public string Svc { get; set; } = string.Empty;

    [SearchableField(IsFilterable = true, IsFacetable = true, AnalyzerName = LexicalAnalyzerName.Values.EnLucene)]
    public required string[] Subjects { get; set; }

    [SearchableField(AnalyzerName = LexicalAnalyzerName.Values.EnLucene, IsFilterable = false, IsFacetable = false,
        IsSortable = false, IsHidden = true)]
    public required string PublisherSearchTerms { get; set; }

    [SearchableField(AnalyzerName = LexicalAnalyzerName.Values.EnLucene, IsFilterable = false, IsFacetable = false,
        IsSortable = false, IsHidden = true)]
    public required string EpisodeSearchTerms { get; set; }

    [SimpleField(IsSortable = false, IsFacetable = false, IsFilterable = false)]
    public string? Image { get; set; }

    // Retrievable so flix/search cards and episode pages can show language flags.
    // Still filterable+facetable for subject language chips (English ≈ null).
    [SimpleField(IsFilterable = true, IsFacetable = true)]
    public string? Lang { get; set; }
}