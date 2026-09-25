using FluentAssertions;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using RedditPodcastPoster.Search.Formatting;
using RedditPodcastPoster.Search.Indexing;
using RedditPodcastPoster.Search.Models;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class PlayableSearchSqlRules
{
    [Fact(DisplayName =
        "Sibling search SQL omits series name and series description for Film, maps the parent name for TV and news, " +
        "and includes image, svc, and the release dual-key, with the description cap Constants.DescriptionSize, " +
        "because every playable card uses the same index columns.")]
    public void sibling_sql_matches_the_shared_card_and_description_cap()
    {
        // Arrange
        var keys = StreamingServiceCatalog.SearchEncodedKeys;
        var images = StreamingServiceCatalog.ImageCoalesceOrder;
        var descriptionSql = DescriptionTruncator.CosmosSql("c.description");
        var seriesDescriptionSql = DescriptionTruncator.CosmosSql("c.publisherDescription");
        var releaseSql = SearchIndexCosmosSql.ReleaseSortOrRelease("c");

        // Act
        var film = PlayableSearchSql.SiblingQuery(
            new PlayableSearchSql.SiblingProjection(SearchContentKind.Film, "c.name", null, IncludeSeries: false),
            keys,
            images);
        var tv = PlayableSearchSql.SiblingQuery(
            new PlayableSearchSql.SiblingProjection(
                SearchContentKind.TvShowEpisode, "c.title", "c.tvShowName", IncludeSeries: true),
            keys,
            images);
        var news = PlayableSearchSql.SiblingQuery(
            new PlayableSearchSql.SiblingProjection(
                SearchContentKind.NewsReport, "c.title", "c.newsOrganisationName", IncludeSeries: true),
            keys,
            images);
        var episodeColumns = PlayableSearchSql.EpisodeUnifiedColumns();

        // Assert
        film.Should().Contain("c.name as title");
        film.Should().NotContain("seriesName");
        film.Should().NotContain("seriesDescription");
        film.Should().Contain("\"\" as podcastName");
        AssertCardColumns(film, descriptionSql, releaseSql);
        film.Should().NotContain(seriesDescriptionSql);

        tv.Should().Contain("c.tvShowName as seriesName");
        tv.Should().Contain("c.tvShowName as podcastName");
        tv.Should().Contain($"{seriesDescriptionSql} as seriesDescription");
        AssertCardColumns(tv, descriptionSql, releaseSql);

        news.Should().Contain("c.newsOrganisationName as seriesName");
        news.Should().Contain("c.newsOrganisationName as podcastName");
        news.Should().Contain($"{seriesDescriptionSql} as seriesDescription");
        AssertCardColumns(news, descriptionSql, releaseSql);

        episodeColumns.Should().Contain($"'{SearchContentKind.Episode}' as contentKind");
        episodeColumns.Should().Contain("e.podcastName as seriesName");
        episodeColumns.Should().Contain($"{DescriptionTruncator.CosmosSql("e.description")} as description");
        episodeColumns.Should().Contain($"{DescriptionTruncator.CosmosSql("e.publisherDescription")} as seriesDescription");
        SearchIndexCosmosSql.ReleaseSortOrRelease("e").Should().Be(Playable.CosmosReleaseSortOrReleaseSql);
    }

    private static void AssertCardColumns(string sql, string descriptionSql, string releaseSql)
    {
        sql.Should().Contain($"{descriptionSql} as description");
        sql.Should().Contain($"{descriptionSql} as episodeDescription");
        sql.Should().Contain(Constants.DescriptionSize.ToString());
        sql.Should().Contain($"{releaseSql} as release");
        sql.Should().Contain(" as svc");
        sql.Should().Contain(" as image");
        sql.Should().Contain("c.services.");
        sql.Should().Contain("?? \"\"");
        sql.Should().Contain("\u2026");
    }
}
