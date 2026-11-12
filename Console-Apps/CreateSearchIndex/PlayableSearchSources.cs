using RedditPodcastPoster.Persistence.Configuration;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using RedditPodcastPoster.Search.Indexing;
using RedditPodcastPoster.Search.Models;

namespace CreateSearchIndex;

/// <summary>
/// Cosmos pull queries for the scheduled sibling playables (TV show episodes and news reports).
/// <c>title</c>, <c>seriesName</c>, and <c>description</c> are the only title fields.
/// <para>
/// The standard service layout is exactly three data sources and three indexers (the Free-tier limit):
/// episodes (<c>--datasource</c> / <c>--indexer</c>), TV show episodes and news reports. Films have no pull indexer;
/// the FilmSearchPusher console pushes them by Cosmos <c>_ts</c>. <see cref="RetiredFilm"/> names the old Film pair
/// so a run can delete it if an earlier version left it behind.
/// </para>
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
        return
        [
            Build(SearchContentKind.TvShowEpisode, settings.TvShowEpisodesContainer, indexName, "c.title", "c.tvShowName", includeSeries: true),
            Build(SearchContentKind.NewsReport, settings.NewsReportsContainer, indexName, "c.title", "c.newsOrganisationName", includeSeries: true)
        ];
    }

    /// <summary>Names of the Film data source and indexer that earlier versions created (no longer scheduled).</summary>
    internal static (string DataSourceName, string IndexerName) RetiredFilm(string indexName)
    {
        var slug = SearchContentKind.Film.ToLowerInvariant();
        return ($"{indexName}-{slug}-ds", $"{indexName}-{slug}-indexer");
    }

    private static Source Build(
        string contentKind,
        string containerName,
        string indexName,
        string titleExpr,
        string seriesNameExpr,
        bool includeSeries)
    {
        var slug = contentKind.ToLowerInvariant();
        var query = PlayableSearchSql.SiblingQuery(
            new PlayableSearchSql.SiblingProjection(contentKind, titleExpr, seriesNameExpr, includeSeries),
            StreamingServiceCatalog.SearchEncodedKeys,
            StreamingServiceCatalog.ImageCoalesceOrder);
        return new Source(
            contentKind,
            containerName,
            $"{indexName}-{slug}-ds",
            $"{indexName}-{slug}-indexer",
            query);
    }
}
