using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

namespace RedditPodcastPoster.Search;

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

    [SimpleField(IsFilterable = false, IsSortable = false, IsFacetable = false)]
    public required bool Explicit { get; set; }

    [SimpleField(IsSortable = false, IsFilterable = false, IsFacetable = false)]
    public required string Spotify { get; set; }

    [SimpleField(IsSortable = false, IsFilterable = false, IsFacetable = false)]
    public required string Apple { get; set; }

    [SimpleField(IsSortable = false, IsFilterable = false, IsFacetable = false)]
    public required string Youtube { get; set; }

    [SimpleField(IsSortable = false, IsFilterable = false, IsFacetable = false)]
    public required string BBC { get; set; }

    [SimpleField(IsSortable = false, IsFilterable = false, IsFacetable = false)]
    public required string InternetArchive { get; set; }

    [SearchableField(IsFilterable = true, IsFacetable = true, AnalyzerName = LexicalAnalyzerName.Values.EnLucene)]
    public required string[] Subjects { get; set; }

    [SearchableField(AnalyzerName = LexicalAnalyzerName.Values.EnLucene, IsFilterable = false, IsFacetable = false,
        IsSortable = false, IsHidden = true)]
    public required string PodcastSearchTerms { get; set; }

    [SearchableField(AnalyzerName = LexicalAnalyzerName.Values.EnLucene, IsFilterable = false, IsFacetable = false,
        IsSortable = false, IsHidden = true)]
    public required string EpisodeSearchTerms { get; set; }

    [SimpleField(IsSortable = false, IsFacetable = false, IsFilterable = false)]
    public required string Image { get; set; }
}