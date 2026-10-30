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
    /// Apple podcast (show) id from <c>services.apple.url</c>. See <see cref="ApplePodcastIdFromUrlExpression"/>.
    /// </summary>
    public static string ApplePodcastIdFromUrl(string documentAlias = "c") =>
        ApplePodcastIdFromUrlExpression($"{documentAlias}.services.apple.url");

    /// <summary>
    /// Apple podcast (show) id from an Apple episode URL expression: the text of the last <c>/id</c> path segment
    /// before <c>?i=</c> (<c>…/podcast/{slug}/id{podcastId}?i={episodeId}</c>). Shared by the Episodes data source
    /// and <see cref="SiblingQuery"/>. Cosmos has no <c>LAST_INDEX_OF</c>, so the part before <c>?i=</c> is reversed and
    /// cut at the first <c>di/</c>. Both cuts use <c>SUBSTRING</c>: <c>INDEX_OF</c> is <c>-1</c> when <c>?i=</c> or
    /// <c>/id</c> is missing, and a negative <c>SUBSTRING</c> length is <c>""</c> (<c>LEFT</c> does not document a
    /// negative length). <c>cut &gt; 0</c> keeps an empty id null. A slug that itself starts with "id"
    /// (<c>identity-crisis</c>, <c>idaho-…</c>) cannot be taken for the id, and a slug-less
    /// <c>/podcast/id{podcastId}?i=</c> still works. Null when there is no <c>?i=</c> or no <c>/id</c> before it.
    /// <see cref="PlayableServiceSearchIds.ApplePodcastId"/> is the C# twin.
    /// </summary>
    public static string ApplePodcastIdFromUrlExpression(string urlExpression)
    {
        // SUBSTRING, not LEFT: INDEX_OF is -1 when ?i= or /id is missing, and only SUBSTRING
        // documents a negative length (it returns ""). cut > 0 keeps that empty id null.
        var beforeEpisode = $"SUBSTRING({urlExpression}, 0, INDEX_OF({urlExpression}, \"?i=\"))";
        var reversed = $"REVERSE({beforeEpisode})";
        var cut = $"INDEX_OF({reversed}, \"di/\")";
        return $"""
            IIF(IS_DEFINED({urlExpression}) AND IS_STRING({urlExpression}) AND CONTAINS({urlExpression}, "?i="),
                IIF({cut} > 0, REVERSE(SUBSTRING({reversed}, 0, {cut})), null),
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
        var release = SearchIndexCosmosSql.ReleaseSort(documentAlias);
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
            WHERE ((NOT IS_DEFINED({documentAlias}.parentRemoved) OR IS_NULL({documentAlias}.parentRemoved) OR {documentAlias}.parentRemoved = false)
                AND (NOT IS_DEFINED({documentAlias}.removed) OR {documentAlias}.removed = false))
              AND {documentAlias}._ts >= @HighWaterMark
            ORDER BY {documentAlias}._ts
            """;
        return Whitespace.Replace(query, " ").Trim();
    }
}
