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

    /// <summary>Prefix of a stored Spotify episode URL (<c>services.spotify.url</c>).</summary>
    public const string SpotifyEpisodeUrlPrefix = "https://open.spotify.com/episode/";

    /// <summary>
    /// Spotify episode id from <c>services.spotify.url</c> (<c>https://open.spotify.com/episode/{id}</c>,
    /// optional <c>?si=</c> query dropped). Sibling playables (news, TV) store no <c>ids</c> object, so the
    /// pull path must derive the id the push path (<c>CataloguePlayableSearchDocument.For</c>) indexes.
    /// Null when the URL is absent or not an episode URL.
    /// </summary>
    public static string SpotifyEpisodeIdFromUrl(string documentAlias = "c")
    {
        var url = $"{documentAlias}.services.spotify.url";
        var start = SpotifyEpisodeUrlPrefix.Length;
        return $"""
            IIF(IS_DEFINED({url}) AND IS_STRING({url}) AND STARTSWITH({url}, "{SpotifyEpisodeUrlPrefix}") AND LENGTH({url}) > {start},
                IIF(INDEX_OF({url}, "?") = -1,
                    SUBSTRING({url}, {start}, LENGTH({url}) - {start}),
                    IIF(INDEX_OF({url}, "?") > {start}, SUBSTRING({url}, {start}, INDEX_OF({url}, "?") - {start}), null)),
                null)
            """;
    }

    /// <summary>
    /// Apple episode id from <c>services.apple.url</c>: the digits after <c>?i=</c> (any later <c>&amp;</c>
    /// parameters dropped). Null when the URL is absent or has no <c>?i=</c>.
    /// </summary>
    public static string AppleEpisodeIdFromUrl(string documentAlias = "c")
    {
        var url = $"{documentAlias}.services.apple.url";
        var start = $"(INDEX_OF({url}, \"?i=\") + 3)";
        var amp = $"INDEX_OF({url}, \"&\", {start})";
        return $"""
            IIF(IS_DEFINED({url}) AND IS_STRING({url}) AND CONTAINS({url}, "?i=") AND LENGTH({url}) > {start},
                IIF({amp} = -1,
                    SUBSTRING({url}, {start}, LENGTH({url}) - {start}),
                    IIF({amp} > {start}, SUBSTRING({url}, {start}, {amp} - {start}), null)),
                null)
            """;
    }

    /// <summary>
    /// Apple podcast (show) id from <c>services.apple.url</c>: the digits between <c>/id</c> and <c>?i=</c>.
    /// Same expression as the Episodes data source's <c>podcastAppleId</c> column.
    /// </summary>
    public static string ApplePodcastIdFromUrl(string documentAlias = "c")
    {
        var url = $"{documentAlias}.services.apple.url";
        return $"""
            IIF(IS_DEFINED({url}) AND CONTAINS({url}, "/id") AND CONTAINS({url}, "?i="),
                SUBSTRING({url},
                    INDEX_OF({url}, "/id") + 3,
                    INDEX_OF({url}, "?i=") - INDEX_OF({url}, "/id") - 3),
                null)
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
        var spotifyId = SpotifyEpisodeIdFromUrl(documentAlias);
        var appleId = AppleEpisodeIdFromUrl(documentAlias);
        var podcastAppleId = ApplePodcastIdFromUrl(documentAlias);
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
                IIF(IS_DEFINED({documentAlias}.youTubeId) AND {documentAlias}.youTubeId != "", {documentAlias}.youTubeId, null) as youtubeId,
                {spotifyId} as spotifyId,
                {appleId} as appleId,
                {podcastAppleId} as podcastAppleId,
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
