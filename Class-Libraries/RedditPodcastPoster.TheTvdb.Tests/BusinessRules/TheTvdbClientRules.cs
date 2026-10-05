using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.TheTvdb.Clients;
using RedditPodcastPoster.TheTvdb.Configuration;
using RedditPodcastPoster.TheTvdb.Models;

namespace RedditPodcastPoster.TheTvdb.Tests.BusinessRules;

public class TheTvdbClientRules
{
    private readonly AutoMocker _mocker = new();
    private readonly DomainTestFixture _fixture = new();
    private readonly StubHandler _handler = new();

    public TheTvdbClientRules()
    {
        var http = new HttpClient(_handler) { BaseAddress = new Uri("https://api4.thetvdb.com/v4/") };
        _mocker.Use(http);
        _mocker.Use(Options.Create(new TheTvdbOptions { ApiKey = _fixture.CreateYouTubeId() }));
        _mocker.Use(new TheTvdbLoginSession());
    }

    [Fact(DisplayName =
        "A TheTVDB series search hit maps the slug and IMDb remote id onto the public series page and the IMDb title page.")]
    public async Task series_search_maps_canonical_urls()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var slug = _fixture.CreateYouTubeId();
        var seriesId = _fixture.CreateAppleId();
        var imdbId = "tt" + _fixture.CreateAppleId();
        _handler.Enqueue(HttpStatusCode.OK, LoginJson());
        _handler.Enqueue(HttpStatusCode.OK, SearchJson(name, slug, seriesId, "series", imdbId));
        var sut = _mocker.CreateInstance<TheTvdbClient>();

        // Act
        var hits = await sut.SearchAsync(name, TheTvdbSearchType.Series);

        // Assert
        hits.Should().ContainSingle();
        hits[0].Id.Should().Be(seriesId);
        hits[0].Name.Should().Be(name);
        hits[0].Type.Should().Be(TheTvdbSearchType.Series);
        hits[0].CanonicalUrl.Should().Be(new Uri($"https://www.thetvdb.com/series/{slug}"));
        hits[0].ImdbUrl.Should().Be(new Uri($"https://www.imdb.com/title/{imdbId}/"));
    }

    [Fact(DisplayName =
        "A TheTVDB movie search hit maps the slug onto the public movie page, because a film is not a series.")]
    public async Task movie_search_maps_movie_page()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var slug = _fixture.CreateYouTubeId();
        var movieId = _fixture.CreateAppleId();
        _handler.Enqueue(HttpStatusCode.OK, LoginJson());
        _handler.Enqueue(HttpStatusCode.OK, SearchJson(name, slug, movieId, "movie", imdbId: null));
        var sut = _mocker.CreateInstance<TheTvdbClient>();

        // Act
        var hits = await sut.SearchAsync(name, TheTvdbSearchType.Movie);

        // Assert
        hits.Should().ContainSingle();
        hits[0].CanonicalUrl.Should().Be(new Uri($"https://www.thetvdb.com/movies/{slug}"));
        hits[0].ImdbUrl.Should().BeNull();
    }

    [Fact(DisplayName =
        "TheTVDB login omits pin when it is unset, because only a subscriber-supported key sends a pin.")]
    public async Task login_omits_empty_pin()
    {
        // Arrange
        _handler.Enqueue(HttpStatusCode.OK, LoginJson());
        _handler.Enqueue(HttpStatusCode.OK, """{"data":[]}""");
        var sut = _mocker.CreateInstance<TheTvdbClient>();

        // Act
        await sut.SearchAsync(_fixture.CreateTitle(), TheTvdbSearchType.Series);

        // Assert
        using var login = JsonDocument.Parse(_handler.Bodies[0]!);
        login.RootElement.TryGetProperty("pin", out _).Should().BeFalse();
        login.RootElement.GetProperty("apikey").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName =
        "A TheTVDB episode list uses the series slug and episode id for the episode page, not the series page.")]
    public async Task episode_list_maps_episode_page()
    {
        // Arrange
        var seriesId = _fixture.CreateAppleId();
        var episodeId = _fixture.CreateAppleId();
        var slug = _fixture.CreateYouTubeId();
        var episodeName = _fixture.CreateTitle();
        _handler.Enqueue(HttpStatusCode.OK, LoginJson());
        _handler.Enqueue(HttpStatusCode.OK, EpisodePageJson(seriesId, slug, episodeId, episodeName));
        _handler.Enqueue(HttpStatusCode.OK, """{"data":{"series":{},"episodes":[]}}""");
        var sut = _mocker.CreateInstance<TheTvdbClient>();

        // Act
        var episodes = await sut.GetEpisodesAsync(seriesId);

        // Assert
        episodes.Should().ContainSingle();
        episodes[0].Id.Should().Be(episodeId);
        episodes[0].Name.Should().Be(episodeName);
        episodes[0].CanonicalUrl.Should().Be(
            new Uri($"https://www.thetvdb.com/series/{slug}/episodes/{episodeId}"));
    }

    [Fact(DisplayName =
        "A TheTVDB series extended record maps the IMDb id and the TheMovieDB.com title id, and ignores an empty id and a collection id that shares that source name.")]
    public async Task series_extended_maps_imdb_and_tmdb_title_id()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var slug = _fixture.CreateYouTubeId();
        var seriesId = _fixture.CreateAppleId();
        var imdbId = "tt" + _fixture.CreateAppleId();
        var tmdbSeriesId = CreateTmdbId();
        var collectionId = CreateDistinctTmdbId(tmdbSeriesId);
        _handler.Enqueue(HttpStatusCode.OK, LoginJson());
        _handler.Enqueue(HttpStatusCode.OK, SeriesExtendedJson(seriesId, name, slug, imdbId, tmdbSeriesId, collectionId));
        var sut = _mocker.CreateInstance<TheTvdbClient>();

        // Act
        var series = await sut.GetSeriesAsync(seriesId);

        // Assert
        series.Should().NotBeNull();
        series!.Id.Should().Be(seriesId);
        series.Name.Should().Be(name);
        series.ImdbTitleId.Should().Be(imdbId);
        series.TmdbSeriesId.Should().Be(tmdbSeriesId);
        series.TmdbSeriesId.Should().NotBe(collectionId);
        series.CanonicalUrl.Should().Be(new Uri($"https://www.thetvdb.com/series/{slug}"));
        series.ImdbUrl.Should().Be(new Uri($"https://www.imdb.com/title/{imdbId}/"));
        _handler.Requests.Should().Contain(request =>
            request.RequestUri!.AbsolutePath.EndsWith($"/series/{seriesId}/extended", StringComparison.Ordinal));
    }

    [Fact(DisplayName =
        "A TheTVDB episode extended record maps the episode IMDb id and the episode TheMovieDB.com id, and takes the parent series TheMovieDB.com id from the series record.")]
    public async Task episode_extended_maps_imdb_episode_tmdb_id_and_parent_series_tmdb_id()
    {
        // Arrange
        var episodeName = _fixture.CreateTitle();
        var slug = _fixture.CreateYouTubeId();
        var seriesId = _fixture.CreateAppleId();
        var episodeId = _fixture.CreateAppleId();
        var imdbId = "tt" + _fixture.CreateAppleId();
        var seriesTmdbId = CreateTmdbId();
        var episodeTmdbId = CreateDistinctTmdbId(seriesTmdbId);
        var collectionId = CreateDistinctTmdbId(seriesTmdbId, episodeTmdbId);
        var season = CreateTmdbId() % 20 + 1;
        var number = CreateTmdbId() % 40 + 1;
        _handler.Enqueue(HttpStatusCode.OK, LoginJson());
        _handler.Enqueue(
            HttpStatusCode.OK,
            EpisodeExtendedJson(episodeId, seriesId, episodeName, season, number, imdbId, episodeTmdbId));
        _handler.Enqueue(
            HttpStatusCode.OK,
            SeriesExtendedJson(seriesId, _fixture.CreateTitle(), slug, imdbId: null, seriesTmdbId, collectionId));
        var sut = _mocker.CreateInstance<TheTvdbClient>();

        // Act
        var episode = await sut.GetEpisodeAsync(episodeId);

        // Assert
        episode.Should().NotBeNull();
        episode!.Id.Should().Be(episodeId);
        episode.SeriesId.Should().Be(seriesId);
        episode.Name.Should().Be(episodeName);
        episode.SeasonNumber.Should().Be(season);
        episode.EpisodeNumber.Should().Be(number);
        episode.ImdbTitleId.Should().Be(imdbId);
        episode.TmdbEpisodeId.Should().Be(episodeTmdbId);
        episode.TmdbSeriesId.Should().Be(seriesTmdbId);
        episode.TmdbSeriesId.Should().NotBe(episodeTmdbId);
        episode.TmdbSeriesId.Should().NotBe(collectionId);
        episode.CanonicalUrl.Should().Be(
            new Uri($"https://www.thetvdb.com/series/{slug}/episodes/{episodeId}"));
        _handler.Requests.Should().Contain(request =>
            request.RequestUri!.AbsolutePath.EndsWith($"/episodes/{episodeId}/extended", StringComparison.Ordinal));
        _handler.Requests.Should().Contain(request =>
            request.RequestUri!.AbsolutePath.EndsWith($"/series/{seriesId}/extended", StringComparison.Ordinal));
        _handler.Requests.Should().NotContain(request =>
            request.RequestUri!.AbsolutePath.EndsWith($"/series/{seriesId}", StringComparison.Ordinal));
    }

    [Fact(DisplayName =
        "A second TheTVDB client reuses the login token from the shared session, because the token is not stored on the transient client.")]
    public async Task second_client_reuses_login_session()
    {
        // Arrange
        var session = new TheTvdbLoginSession();
        var apiKey = _fixture.CreateYouTubeId();
        var query = _fixture.CreateTitle();
        var firstHandler = new StubHandler();
        firstHandler.Enqueue(HttpStatusCode.OK, LoginJson());
        firstHandler.Enqueue(HttpStatusCode.OK, """{"data":[]}""");
        var secondHandler = new StubHandler();
        secondHandler.Enqueue(HttpStatusCode.OK, """{"data":[]}""");
        var first = CreateClient(session, apiKey, firstHandler);
        var second = CreateClient(session, apiKey, secondHandler);

        // Act
        await first.SearchAsync(query, TheTvdbSearchType.Series);
        await second.SearchAsync(query, TheTvdbSearchType.Series);

        // Assert
        firstHandler.Requests.Should().HaveCount(2);
        firstHandler.Requests[0].RequestUri!.AbsolutePath.Should().EndWith("/login");
        secondHandler.Requests.Should().ContainSingle();
        secondHandler.Requests[0].RequestUri!.AbsolutePath.Should().EndWith("/search");
        secondHandler.Requests[0].Headers.Authorization!.Scheme.Should().Be("Bearer");
        secondHandler.Requests[0].Headers.Authorization!.Parameter.Should().Be("issued-token");
    }

    private TheTvdbClient CreateClient(TheTvdbLoginSession session, string apiKey, StubHandler handler)
    {
        var mocker = new AutoMocker();
        mocker.Use(new HttpClient(handler) { BaseAddress = new Uri("https://api4.thetvdb.com/v4/") });
        mocker.Use(Options.Create(new TheTvdbOptions { ApiKey = apiKey }));
        mocker.Use(session);
        return mocker.CreateInstance<TheTvdbClient>();
    }

    private int CreateTmdbId()
    {
        var value = (int)(_fixture.CreateAppleId() % 1_000_000_000L);
        return value == 0 ? 1 : value;
    }

    private int CreateDistinctTmdbId(params int[] reserved)
    {
        var value = CreateTmdbId();
        while (reserved.Contains(value))
        {
            value = value == int.MaxValue ? 1 : value + 1;
        }

        return value;
    }

    private static string LoginJson() => """{"data":{"token":"issued-token"}}""";

    private static string SearchJson(string name, string slug, long id, string type, string? imdbId)
    {
        var remoteIds = imdbId is null
            ? new JsonArray()
            : new JsonArray { new JsonObject { ["id"] = imdbId, ["sourceName"] = "IMDB" } };
        var payload = new JsonObject
        {
            ["data"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = name,
                    ["slug"] = slug,
                    ["tvdb_id"] = id.ToString(),
                    ["type"] = type,
                    ["remote_ids"] = remoteIds
                }
            }
        };
        return payload.ToJsonString();
    }

    private static string EpisodePageJson(long seriesId, string slug, long episodeId, string name)
    {
        var payload = new JsonObject
        {
            ["data"] = new JsonObject
            {
                ["series"] = new JsonObject { ["id"] = seriesId, ["slug"] = slug },
                ["episodes"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = episodeId,
                        ["seriesId"] = seriesId,
                        ["name"] = name,
                        ["seasonNumber"] = 1,
                        ["number"] = 1
                    }
                }
            }
        };
        return payload.ToJsonString();
    }

    private static string SeriesExtendedJson(
        long id,
        string name,
        string slug,
        string? imdbId,
        int tmdbSeriesId,
        int collectionId)
    {
        var remoteIds = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "",
                ["type"] = 12,
                ["sourceName"] = "TheMovieDB.com"
            },
            new JsonObject
            {
                ["id"] = collectionId.ToString(),
                ["type"] = 28,
                ["sourceName"] = "TheMovieDB.com"
            },
            new JsonObject
            {
                ["id"] = tmdbSeriesId.ToString(),
                ["type"] = 12,
                ["sourceName"] = "TheMovieDB.com"
            }
        };
        if (imdbId is not null)
        {
            remoteIds.Insert(0, new JsonObject
            {
                ["id"] = imdbId,
                ["type"] = 2,
                ["sourceName"] = "IMDB"
            });
        }

        var payload = new JsonObject
        {
            ["data"] = new JsonObject
            {
                ["id"] = id,
                ["name"] = name,
                ["slug"] = slug,
                ["remoteIds"] = remoteIds
            }
        };
        return payload.ToJsonString();
    }

    private static string EpisodeExtendedJson(
        long episodeId,
        long seriesId,
        string name,
        int season,
        int number,
        string imdbId,
        int episodeTmdbId)
    {
        var payload = new JsonObject
        {
            ["data"] = new JsonObject
            {
                ["id"] = episodeId,
                ["seriesId"] = seriesId,
                ["name"] = name,
                ["seasonNumber"] = season,
                ["number"] = number,
                ["remoteIds"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = imdbId,
                        ["type"] = 2,
                        ["sourceName"] = "IMDB"
                    },
                    new JsonObject
                    {
                        ["id"] = episodeTmdbId.ToString(),
                        ["type"] = 12,
                        ["sourceName"] = "TheMovieDB.com"
                    }
                }
            }
        };
        return payload.ToJsonString();
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string?> Bodies { get; } = [];

        public StubHandler Enqueue(HttpStatusCode status, string json)
        {
            _responses.Enqueue(new HttpResponseMessage(status)
            {
                Content = new StringContent(json)
            });
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken));
            return _responses.Dequeue();
        }
    }
}
