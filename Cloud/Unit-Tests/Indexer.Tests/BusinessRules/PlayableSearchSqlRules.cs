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
        "Sibling search SQL omits series name for Film, maps the parent name for TV and news, " +
        "and includes image, svc, youtubeId from c.youTubeId, and the release dual-key, with the description cap Constants.DescriptionSize, " +
        "because every playable card uses title, seriesName, and description only.")]
    public void sibling_sql_matches_the_shared_card_and_description_cap()
    {
        // Arrange
        var keys = StreamingServiceCatalog.SearchEncodedKeys;
        var images = StreamingServiceCatalog.ImageCoalesceOrder;
        var descriptionSql = DescriptionTruncator.CosmosSql("c.description");
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
        film.Should().NotContain("episodeTitle");
        film.Should().NotContain("episodeDescription");
        AssertCardColumns(film, descriptionSql, releaseSql);

        tv.Should().Contain("c.tvShowName as seriesName");
        tv.Should().NotContain("podcastName");
        AssertCardColumns(tv, descriptionSql, releaseSql);

        news.Should().Contain("c.newsOrganisationName as seriesName");
        news.Should().NotContain("podcastName");
        AssertCardColumns(news, descriptionSql, releaseSql);

        episodeColumns.Should().Contain($"'{SearchContentKind.Episode}' as contentKind");
        episodeColumns.Should().Contain("e.title as title");
        episodeColumns.Should().Contain("e.podcastName as seriesName");
        episodeColumns.Should().Contain($"{DescriptionTruncator.CosmosSql("e.description")} as description");
        episodeColumns.Should().NotContain("episodeTitle");
        episodeColumns.Should().NotContain("seriesDescription");
        SearchIndexCosmosSql.ReleaseSortOrRelease("e").Should().Be(Playable.CosmosReleaseSortOrReleaseSql);
    }

    private static void AssertCardColumns(string sql, string descriptionSql, string releaseSql)
    {
        sql.Should().Contain($"{descriptionSql} as description");
        sql.Should().NotContain("episodeDescription");
        sql.Should().Contain(Constants.DescriptionSize.ToString());
        sql.Should().Contain($"{releaseSql} as release");
        sql.Should().Contain(" as svc");
        sql.Should().Contain(" as image");
        sql.Should().Contain(" as youtubeId");
        sql.Should().Contain("c.youTubeId");
        sql.Should().Contain(" as spotifyId");
        sql.Should().Contain(" as appleId");
        sql.Should().Contain(" as podcastAppleId");
        sql.Should().Contain("c.services.");
        sql.Should().Contain("?? \"\"");
        sql.Should().Contain("\u2026");
    }

    [Fact(DisplayName =
        "News and TV pull SQL projects spotifyId, appleId and podcastAppleId from services.*.url, " +
        "because those documents store no ids object and the website draws the Spotify and Apple icons only from these fields; " +
        "without them every pull-indexed report shows no Spotify or Apple icon until a curator edit pushes it.")]
    public void sibling_sql_projects_spotify_and_apple_ids_from_service_urls()
    {
        // Arrange
        var keys = StreamingServiceCatalog.SearchEncodedKeys;
        var images = StreamingServiceCatalog.ImageCoalesceOrder;

        // Act
        var queries = new[]
        {
            PlayableSearchSql.SiblingQuery(
                new PlayableSearchSql.SiblingProjection(
                    SearchContentKind.TvShowEpisode, "c.title", "c.tvShowName", IncludeSeries: true),
                keys,
                images),
            PlayableSearchSql.SiblingQuery(
                new PlayableSearchSql.SiblingProjection(
                    SearchContentKind.NewsReport, "c.title", "c.newsOrganisationName", IncludeSeries: true),
                keys,
                images)
        };

        // Assert
        foreach (var sql in queries)
        {
            sql.Should().Contain($"{Normalise(PlayableSearchSql.SpotifyEpisodeIdFromUrl("c"))} as spotifyId");
            sql.Should().Contain($"{Normalise(PlayableSearchSql.AppleEpisodeIdFromUrl("c"))} as appleId");
            sql.Should().Contain($"{Normalise(PlayableSearchSql.ApplePodcastIdFromUrl("c"))} as podcastAppleId");
            sql.Should().Contain("c.services.spotify.url");
            sql.Should().Contain("c.services.apple.url");
            sql.Should().NotContain("c.ids.");
        }
    }

    [Fact(DisplayName =
        "The Spotify id expression only accepts open.spotify.com/episode URLs and cuts any query string, " +
        "so a show URL, a share link's ?si= suffix or a missing service never becomes a wrong spotifyId.")]
    public void spotify_id_expression_is_scoped_to_episode_urls()
    {
        // Act
        var sql = PlayableSearchSql.SpotifyEpisodeIdFromUrl("c");

        // Assert
        PlayableSearchSql.SpotifyEpisodeUrlPrefix.Should().Be("https://open.spotify.com/episode/");
        sql.Should().Contain("IS_DEFINED(c.services.spotify.url)");
        sql.Should().Contain("STARTSWITH(c.services.spotify.url, \"https://open.spotify.com/episode/\")");
        sql.Should().Contain($"SUBSTRING(c.services.spotify.url, {PlayableSearchSql.SpotifyEpisodeUrlPrefix.Length},");
        sql.Should().Contain("INDEX_OF(c.services.spotify.url, \"?\")");
        sql.TrimEnd().Should().EndWith("null)");
    }

    [Fact(DisplayName =
        "Apple ids come from the episode URL: appleId is the ?i= value and podcastAppleId is the /id segment, " +
        "matching the Episodes data source so the Apple icon gets the same pair on every content kind.")]
    public void apple_id_expressions_mirror_the_episode_datasource()
    {
        // Act
        var appleId = PlayableSearchSql.AppleEpisodeIdFromUrl("c");
        var podcastAppleId = PlayableSearchSql.ApplePodcastIdFromUrl("c");

        // Assert
        appleId.Should().Contain("CONTAINS(c.services.apple.url, \"?i=\")");
        appleId.Should().Contain("(INDEX_OF(c.services.apple.url, \"?i=\") + 3)");
        appleId.Should().Contain("INDEX_OF(c.services.apple.url, \"&\",");
        appleId.TrimEnd().Should().EndWith("null)");
        Normalise(podcastAppleId).Should().Be(Normalise(
            """
            IIF(IS_DEFINED(c.services.apple.url) AND CONTAINS(c.services.apple.url, "/id") AND CONTAINS(c.services.apple.url, "?i="),
                SUBSTRING(c.services.apple.url,
                    INDEX_OF(c.services.apple.url, "/id") + 3,
                    INDEX_OF(c.services.apple.url, "?i=") - INDEX_OF(c.services.apple.url, "/id") - 3),
                null)
            """));
    }

    private static string Normalise(string sql) =>
        System.Text.RegularExpressions.Regex.Replace(sql, @"\s+", " ").Trim();
}
