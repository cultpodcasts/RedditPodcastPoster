using RedditPodcastPoster.Persistence.Configuration;
using RedditPodcastPoster.Search.Formatting;
using RedditPodcastPoster.Search.Models;

namespace CreateSearchIndex;

/// <summary>
/// Cosmos pull queries for playables other than Episodes. Parent description is
/// not on these documents, so <c>seriesDescription</c> is left unset. Film omits
/// series fields. Legacy episodeTitle / podcastName / episodeDescription stay
/// filled so the shared index schema still has those required columns.
/// </summary>
internal static class PlayableSearchSources
{
    internal readonly record struct Source(
        string ContentKind,
        string ContainerName,
        string DataSourceName,
        string IndexerName,
        string Query);

    internal static IReadOnlyList<Source> Siblings(CosmosDbSettings settings, string indexName)
    {
        var cap = Constants.DescriptionSize;
        return
        [
            Build(
                SearchContentKind.TvShowEpisode,
                settings.TvShowEpisodesContainer,
                indexName,
                "c.title",
                "c.tvShowName",
                includeSeries: true,
                cap),
            Build(
                SearchContentKind.Film,
                settings.FilmsContainer,
                indexName,
                "c.name",
                seriesNameExpr: "\"\"",
                includeSeries: false,
                cap),
            Build(
                SearchContentKind.NewsReport,
                settings.NewsReportsContainer,
                indexName,
                "c.title",
                "c.newsOrganisationName",
                includeSeries: true,
                cap)
        ];
    }

    private static Source Build(
        string contentKind,
        string containerName,
        string indexName,
        string titleExpr,
        string seriesNameExpr,
        bool includeSeries,
        int cap)
    {
        var slug = contentKind.ToLowerInvariant();
        var truncated = Truncated("c.description", cap);
        var seriesNameSelect = includeSeries
            ? $"{seriesNameExpr} as seriesName, {seriesNameExpr} as podcastName,"
            : "\"\" as podcastName,";
        var query = $"""
            SELECT
                c.id,
                '{contentKind}' as contentKind,
                {titleExpr} as title,
                {titleExpr} as episodeTitle,
                {seriesNameSelect}
                {truncated} as description,
                {truncated} as episodeDescription,
                c.releaseSort as release,
                IIF(ENDSWITH(c.duration, ".0000000"), SUBSTRING(c.duration, 0, LENGTH(c.duration) - 8), c.duration) as duration,
                "" as bbc,
                "" as internetArchive,
                c.subjects as subjects,
                c.publisherSearchTerms as publisherSearchTerms,
                c.searchTerms as episodeSearchTerms,
                c.lang as lang,
                c._ts
            FROM c
            WHERE ((NOT IS_DEFINED(c.parentRemoved) OR c.parentRemoved = false)
                AND (NOT IS_DEFINED(c.removed) OR c.removed = false))
              AND c._ts >= @HighWaterMark
            ORDER BY c._ts
            """;
        return new Source(
            contentKind,
            containerName,
            $"{indexName}-{slug}-ds",
            $"{indexName}-{slug}-indexer",
            Whitespace.Replace(query, " ").Trim());
    }

    private static string Truncated(string expr, int cap) =>
        $"IIF(LENGTH({expr}) > {cap}, CONCAT(SUBSTRING({expr}, 0, {cap - 1}), \"\u2026\"), {expr})";

    private static readonly System.Text.RegularExpressions.Regex Whitespace =
        new(@"\s+", System.Text.RegularExpressions.RegexOptions.Compiled);
}
