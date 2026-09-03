using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

namespace RedditPodcastPoster.Search.Models;

public class EpisodeSearchRecord
{
    [SimpleField(IsKey = true, IsFilterable = true, IsSortable = false, IsFacetable = false)]
    public required string Id { get; set; }

    [SearchableField(AnalyzerName = LexicalAnalyzerName.Values.EnLucene, IsFilterable = false, IsFacetable = false,
        IsSortable = false)]
    public required string EpisodeTitle { get; set; }

    [SearchableField(IsFilterable = true, IsFacetable = true, AnalyzerName = LexicalAnalyzerName.Values.EnLucene,
        IsSortable = false)]
    public required string PodcastName { get; set; }

    [SearchableField(AnalyzerName = LexicalAnalyzerName.Values.EnLucene, IsFilterable = false, IsSortable = false,
        IsFacetable = false)]
    public required string EpisodeDescription { get; set; }

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
    public required string PodcastSearchTerms { get; set; }

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