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
        // Arrange
        const string alias = "c";

        // Act
        var sql = PlayableSearchSql.SpotifyEpisodeIdFromUrl(alias);

        // Assert
        PlayableSearchSql.SpotifyEpisodeUrlPrefix.Should().Be("https://open.spotify.com/episode/");
        sql.Should().Contain("IS_DEFINED(c.services.spotify.url)");
        sql.Should().Contain("STARTSWITH(c.services.spotify.url, \"https://open.spotify.com/episode/\")");
        sql.Should().Contain($"SUBSTRING(c.services.spotify.url, {PlayableSearchSql.SpotifyEpisodeUrlPrefix.Length},");
        sql.Should().Contain("INDEX_OF(c.services.spotify.url, \"?\")");
        sql.TrimEnd().Should().EndWith("null)");
    }

    [Fact(DisplayName =
        "Apple ids come from the episode URL: appleId is the ?i= value and podcastAppleId is the shared /id " +
        "expression the Episodes data source also uses, so the Apple icon gets the same pair on every content kind.")]
    public void apple_id_expressions_mirror_the_episode_datasource()
    {
        // Arrange
        const string alias = "c";

        // Act
        var appleId = PlayableSearchSql.AppleEpisodeIdFromUrl(alias);
        var podcastAppleId = PlayableSearchSql.ApplePodcastIdFromUrl(alias);

        // Assert
        appleId.Should().Contain("CONTAINS(c.services.apple.url, \"?i=\")");
        appleId.Should().Contain("(INDEX_OF(c.services.apple.url, \"?i=\") + 3)");
        appleId.Should().Contain("INDEX_OF(c.services.apple.url, \"&\",");
        appleId.TrimEnd().Should().EndWith("null)");
        podcastAppleId.Should().Be(PlayableSearchSql.ApplePodcastIdFromUrlExpression("c.services.apple.url"));
    }

    [Fact(DisplayName =
        "podcastAppleId is cut from the last /id segment before ?i= (the part before ?i= reversed, up to the first " +
        "di/) with SUBSTRING, so a missing ?i= or /id (INDEX_OF of -1) is an empty prefix and cut > 0 keeps an empty " +
        "id null, and an Apple slug that itself starts with \"id\" (identity-crisis, idaho-...) cannot be taken for " +
        "the show id, which the old first-/id expression got wrong on every content kind.")]
    public void podcast_apple_id_is_anchored_on_the_last_id_segment()
    {
        // Arrange
        const string urlExpression = "e.services.apple.url";

        // Act
        var sql = Normalise(PlayableSearchSql.ApplePodcastIdFromUrlExpression(urlExpression));

        // Assert
        sql.Should().Contain("REVERSE(SUBSTRING(e.services.apple.url, 0, INDEX_OF(e.services.apple.url, \"?i=\")))");
        sql.Should().Contain(
            "INDEX_OF(REVERSE(SUBSTRING(e.services.apple.url, 0, INDEX_OF(e.services.apple.url, \"?i=\"))), \"di/\") > 0");
        sql.Should().NotContain("LEFT(");
        sql.Should().NotContain("INDEX_OF(e.services.apple.url, \"/id\")");
        sql.Should().EndWith("null)");
    }

    private static string Normalise(string sql) =>
        System.Text.RegularExpressions.Regex.Replace(sql, @"\s+", " ").Trim();
}
