using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Playables;
using RedditPodcastPoster.Persistence.Providers;

namespace RedditPodcastPoster.Persistence.Tests;

public class PlayableServiceUrlFilterTranslationRules
{
    private readonly DomainTestFixture _fixture = new();

    private CosmosClient CreateClient() =>
        new(
            "https://synthetic.documents.example:443/",
            Convert.ToBase64String(_fixture.CreateGuid().ToByteArray()),
            new CosmosClientOptions
            {
                UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptionsProvider().GetJsonSerializerOptions()
            });

    [Fact(DisplayName =
        "When films with a Netflix link are projected to their stored Netflix URL, then the Cosmos SQL filters on " +
        "services.netflix and selects only its url, so the #1517 legacy country /title/ lookup never reads whole documents.")]
    public void has_service_link_with_service_url_projection_translates()
    {
        // Arrange
        using var client = CreateClient();
        var container = client.GetContainer("db", "films");

        // Act
        var sql = container.GetItemLinqQueryable<Film>()
            .Where(PlayableServiceUrlFilters.HasServiceLink<Film>("netflix"))
            .Select(PlayableServiceUrlFilters.ServiceUrl<Film>("netflix"))
            .ToQueryDefinition()
            .QueryText;

        // Assert
        sql.Should().Contain("SELECT VALUE");
        sql.Should().Contain("[\"services\"][\"netflix\"]");
        sql.Should().Contain("[\"url\"]");
        sql.Should().Contain("!= null");
    }

    [Fact(DisplayName =
        "When films are filtered by FilmHoldsServiceOrTrailerUrl, then the Cosmos SQL matches the service link or any trailers[].url, because URL membership and the indexer must recognise a trailer URL as already in the catalogue.")]
    public void holds_service_or_trailer_url_translates_to_services_or_trailer_exists()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var pasted = new Uri($"https://youtu.be/{videoId}");
        var stored = new Uri($"https://www.youtube.com/watch?v={videoId}");
        using var client = CreateClient();
        var container = client.GetContainer("db", "films");

        // Act
        var sql = container.GetItemLinqQueryable<Film>()
            .Where(PlayableServiceUrlFilters.FilmHoldsServiceOrTrailerUrl(
                ServiceKeys.YouTube, pasted, stored, youTubeId: videoId))
            .ToQueryDefinition()
            .QueryText;

        // Assert
        sql.Should().Contain("[\"services\"]");
        sql.Should().Contain("EXISTS");
        sql.Should().Contain("[\"trailers\"]");
        sql.Should().Contain("[\"url\"]");
        sql.Should().Contain("[\"ids\"]");
        sql.Should().Contain(stored.ToString());
        sql.Should().Contain(pasted.ToString());
        sql.Should().Contain(videoId);
    }

    [Fact(DisplayName =
        "When films are filtered by FilmHoldsServiceOrTrailerUrl for Spotify with an episode id, then the Cosmos SQL also matches trailers[].services.spotify.url and trailers[].ids.spotify, because a YouTube-sourced trailer stores those so a Spotify re-find still matches (#1223).")]
    public void holds_service_or_trailer_url_for_spotify_matches_trailer_services_and_ids()
    {
        // Arrange
        var spotifyId = _fixture.CreateSpotifyId();
        var pasted = new Uri($"https://open.spotify.com/episode/{spotifyId}?si=synthetic");
        var stored = new Uri($"https://open.spotify.com/episode/{spotifyId}");
        using var client = CreateClient();
        var container = client.GetContainer("db", "films");

        // Act
        var sql = container.GetItemLinqQueryable<Film>()
            .Where(PlayableServiceUrlFilters.FilmHoldsServiceOrTrailerUrl(
                ServiceKeys.Spotify, pasted, stored, spotifyId: spotifyId))
            .ToQueryDefinition()
            .QueryText;

        // Assert
        sql.Should().Contain("[\"trailers\"]");
        sql.Should().Contain("[\"services\"]");
        sql.Should().Contain("[\"spotify\"]");
        sql.Should().Contain("[\"ids\"]");
        sql.Should().Contain(spotifyId);
        sql.Should().Contain(stored.ToString());
        sql.Should().Contain(pasted.ToString());
    }

    [Fact(DisplayName =
        "When films are filtered by FilmHoldsServiceOrTrailerUrl for Apple with an episode id, then the Cosmos SQL also matches trailers[].services.apple.url and trailers[].ids.apple, because a YouTube-sourced trailer stores those so an Apple re-find still matches (#1223).")]
    public void holds_service_or_trailer_url_for_apple_matches_trailer_services_and_ids()
    {
        // Arrange
        var appleId = _fixture.CreateAppleId();
        var path = $"/podcast/synthetic-show/id{_fixture.CreateAppleId()}?i={appleId}";
        var pasted = new Uri($"https://podcasts.apple.com/us{path}&uo=4");
        var stored = new Uri($"https://podcasts.apple.com{path}");
        using var client = CreateClient();
        var container = client.GetContainer("db", "films");

        // Act
        var sql = container.GetItemLinqQueryable<Film>()
            .Where(PlayableServiceUrlFilters.FilmHoldsServiceOrTrailerUrl(
                ServiceKeys.Apple, pasted, stored, appleId: appleId))
            .ToQueryDefinition()
            .QueryText;

        // Assert
        sql.Should().Contain("[\"trailers\"]");
        sql.Should().Contain("[\"services\"]");
        sql.Should().Contain("[\"apple\"]");
        sql.Should().Contain("[\"ids\"]");
        sql.Should().Contain(appleId.ToString());
        sql.Should().Contain(stored.ToString());
        sql.Should().Contain(pasted.ToString());
    }

    [Fact(DisplayName =
        "When films are filtered by FilmHoldsServiceOrTrailerUrl, then the services arm and the trailers arm sit on either side of one top-level OR, because a precedence slip would AND the trailer match with the service link and miss trailers.")]
    public void film_holds_service_or_trailer_url_groups_the_two_arms_under_one_or()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var pasted = new Uri($"https://youtu.be/{videoId}");
        var stored = new Uri($"https://www.youtube.com/watch?v={videoId}");
        using var client = CreateClient();
        var container = client.GetContainer("db", "films");

        // Act
        var sql = container.GetItemLinqQueryable<Film>()
            .Where(PlayableServiceUrlFilters.FilmHoldsServiceOrTrailerUrl(
                ServiceKeys.YouTube, pasted, stored, youTubeId: videoId))
            .ToQueryDefinition()
            .QueryText;

        // Assert
        // The trailer EXISTS is hoisted into a JOIN, so the outer filter is the last WHERE.
        var where = sql[(sql.LastIndexOf("WHERE", StringComparison.Ordinal) + "WHERE".Length)..].Trim();
        var topLevelOr = TopLevelOrIndex(where);
        topLevelOr.Should().BeGreaterThan(0, sql);
        sql.Should().Contain("EXISTS(").And.Contain("IN root[\"trailers\"]");
        where[..topLevelOr].Should().Contain("[\"services\"]", sql).And.NotContain("trailers");
        where[topLevelOr..].Should().Contain("root[\"trailers\"] != null", sql);
    }

    [Fact(DisplayName =
        "When TV episodes are filtered by HoldsServiceUrl, then the Cosmos SQL compares the service link to both the pasted and the stored URL form.")]
    public void holds_service_url_compares_both_url_forms()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var pasted = new Uri($"https://youtu.be/{videoId}");
        var stored = new Uri($"https://www.youtube.com/watch?v={videoId}");
        using var client = CreateClient();
        var container = client.GetContainer("db", "tvShowEpisodes");

        // Act
        var sql = container.GetItemLinqQueryable<TvShowEpisode>()
            .Where(PlayableServiceUrlFilters.HoldsServiceUrl<TvShowEpisode>(ServiceKeys.YouTube, pasted, stored))
            .ToQueryDefinition()
            .QueryText;

        // Assert
        sql.Should().Contain("[\"services\"][\"youtube\"][\"url\"]");
        sql.Should().Contain(pasted.ToString());
        sql.Should().Contain(stored.ToString());
        sql.Should().Contain(" OR ");
    }

    /// <summary>Index of the first " OR " at parenthesis depth 1 (inside the WHERE's outer parentheses), or -1.</summary>
    private static int TopLevelOrIndex(string where)
    {
        var depth = 0;
        for (var i = 0; i < where.Length; i++)
        {
            switch (where[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case ' ' when depth == 1 && string.CompareOrdinal(where, i, " OR ", 0, 4) == 0:
                    return i;
            }
        }

        return -1;
    }


    [Fact(DisplayName =
        "When films are filtered by FilmHoldsServiceOrTrailerUrl for YouTube, then the Cosmos SQL also matches additionalYouTube[].youTubeId, because an alternate upload is already in the catalogue (RPP #1411).")]
    public void holds_service_or_trailer_url_translates_additionalYouTube()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var pasted = new Uri($"https://youtu.be/{videoId}");
        var stored = new Uri($"https://www.youtube.com/watch?v={videoId}");
        using var client = CreateClient();
        var container = client.GetContainer("db", "films");

        // Act
        var sql = container.GetItemLinqQueryable<Film>()
            .Where(PlayableServiceUrlFilters.FilmHoldsServiceOrTrailerUrl(
                ServiceKeys.YouTube, pasted, stored, youTubeId: videoId))
            .ToQueryDefinition()
            .QueryText;

        // Assert
        sql.Should().Contain("[\"additionalYouTube\"]");
        sql.Should().Contain("[\"youTubeId\"]");
        sql.Should().Contain(videoId);
    }

    [Fact(DisplayName =
        "When TV episodes or news reports are filtered by HoldsServiceUrlOrYouTubeAlternate for YouTube, then the Cosmos SQL matches additionalYouTube[].youTubeId (RPP #1411).")]
    public void holds_service_url_or_youtube_alternate_translates_additionalYouTube()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var pasted = new Uri($"https://youtu.be/{videoId}");
        var stored = new Uri($"https://www.youtube.com/watch?v={videoId}");
        using var client = CreateClient();
        var tvSql = client.GetContainer("db", "tv")
            .GetItemLinqQueryable<TvShowEpisode>()
            .Where(PlayableServiceUrlFilters.HoldsServiceUrlOrYouTubeAlternate<TvShowEpisode>(
                ServiceKeys.YouTube, pasted, stored, videoId))
            .ToQueryDefinition()
            .QueryText;
        var newsSql = client.GetContainer("db", "news")
            .GetItemLinqQueryable<NewsReport>()
            .Where(PlayableServiceUrlFilters.HoldsServiceUrlOrYouTubeAlternate<NewsReport>(
                ServiceKeys.YouTube, pasted, stored, videoId))
            .ToQueryDefinition()
            .QueryText;

        // Assert
        tvSql.Should().Contain("[\"additionalYouTube\"]");
        tvSql.Should().Contain("[\"youTubeId\"]");
        newsSql.Should().Contain("[\"additionalYouTube\"]");
        newsSql.Should().Contain(videoId);
    }
}
