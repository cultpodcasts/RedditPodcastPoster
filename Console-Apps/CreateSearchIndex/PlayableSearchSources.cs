using RedditPodcastPoster.Persistence.Configuration;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using RedditPodcastPoster.Search.Indexing;
using RedditPodcastPoster.Search.Models;

namespace CreateSearchIndex;

/// <summary>
/// Cosmos pull queries for playables other than Episodes. Parent blurbs come from
/// denormalised <c>publisherDescription</c>. Film omits series fields. Legacy
/// episodeTitle / podcastName / episodeDescription stay filled so the shared index
/// schema still has those required columns.
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
            Build(SearchContentKind.Film, settings.FilmsContainer, indexName, "c.name", seriesNameExpr: null, includeSeries: false),
            Build(SearchContentKind.NewsReport, settings.NewsReportsContainer, indexName, "c.title", "c.newsOrganisationName", includeSeries: true)
        ];
    }

    private static Source Build(
        string contentKind,
        string containerName,
        string indexName,
        string titleExpr,
        string? seriesNameExpr,
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
