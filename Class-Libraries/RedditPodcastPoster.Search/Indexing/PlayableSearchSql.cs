using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Search.Formatting;
using RedditPodcastPoster.Search.Models;

namespace RedditPodcastPoster.Search.Indexing;

/// <summary>
/// Cosmos SELECT text for the shared playable index. Episode pull columns and sibling
/// documents share <see cref="DescriptionTruncator"/> and <see cref="SearchIndexCosmosSql"/>.
/// </summary>
public static class PlayableSearchSql
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    public readonly record struct SiblingProjection(
        string ContentKind,
        string TitleExpression,
        string? SeriesNameExpression,
        bool IncludeSeries);

    /// <summary>
    /// Replacement columns for the rebuilt index. <c>title</c> replaces
    /// <c>episodeTitle</c>, <c>seriesName</c> replaces <c>podcastName</c>,
    /// and <c>description</c> replaces <c>episodeDescription</c>.
    /// </summary>
    public static string EpisodeUnifiedColumns()
    {
        var description = DescriptionTruncator.CosmosSql("e.description");
        return $"""
            '{SearchContentKind.Episode}' as contentKind,
            e.title as title,
            e.podcastName as seriesName,
            {description} as description
            """;
    }

    public static string SiblingQuery(
        SiblingProjection projection,
        IReadOnlyList<string> svcKeys,
        IReadOnlyList<string> imageCoalesceOrder,
        string documentAlias = "c")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projection.ContentKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(projection.TitleExpression);
        if (projection.IncludeSeries)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projection.SeriesNameExpression);
        }

        var truncated = DescriptionTruncator.CosmosSql($"{documentAlias}.description");
        var seriesNameSelect = projection.IncludeSeries
            ? $"{projection.SeriesNameExpression} as seriesName,"
            : string.Empty;
        var release = SearchIndexCosmosSql.ReleaseSortOrRelease(documentAlias);
        var image = SearchIndexCosmosSql.ImageOrEmpty(imageCoalesceOrder, documentAlias);
        var svc = SearchIndexCosmosSql.SvcProjection(svcKeys, documentAlias);
        var query = $"""
            SELECT
                {documentAlias}.id,
                '{projection.ContentKind}' as contentKind,
                {projection.TitleExpression} as title,
                {seriesNameSelect}
                {truncated} as description,
                {release} as release,
                IIF(ENDSWITH({documentAlias}.duration, ".0000000"), SUBSTRING({documentAlias}.duration, 0, LENGTH({documentAlias}.duration) - 8), {documentAlias}.duration) as duration,
                "" as bbc,
                "" as internetArchive,
                {svc} as svc,
                {image} as image,
                {documentAlias}.subjects as subjects,
                {documentAlias}.publisherSearchTerms as publisherSearchTerms,
                {documentAlias}.searchTerms as episodeSearchTerms,
                {documentAlias}.lang as lang,
                {documentAlias}._ts
            FROM c
            WHERE ((NOT IS_DEFINED({documentAlias}.parentRemoved) OR {documentAlias}.parentRemoved = false)
                AND (NOT IS_DEFINED({documentAlias}.removed) OR {documentAlias}.removed = false))
              AND {documentAlias}._ts >= @HighWaterMark
            ORDER BY {documentAlias}._ts
            """;
        return Whitespace.Replace(query, " ").Trim();
    }
}
