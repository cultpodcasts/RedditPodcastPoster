using System.Net;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Tmdb.Clients;
using RedditPodcastPoster.Tmdb.Configuration;
using RedditPodcastPoster.Catalogue.Identity;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Tmdb.Models;

namespace RedditPodcastPoster.Tmdb.Tests.BusinessRules;

public class TmdbClientRules
{
    private readonly AutoMocker _mocker = new();
    private readonly DomainTestFixture _fixture = new();
    private readonly StubHandler _handler = new();
    private readonly TmdbOptions _options = new();

    public TmdbClientRules()
    {
        _options.ApiKey = _fixture.CreateYouTubeId();
        var http = new HttpClient(_handler) { BaseAddress = new Uri("https://api.themoviedb.org/3/") };
        _mocker.Use(http);
        _mocker.Use(Options.Create(_options));
    }

    [Fact(DisplayName =
        "When TMDB has an IMDb title id, that id is the canonical id even if a TheTVDB id is also present.")]
    public async Task imdb_id_is_canonical_when_present()
    {
        // Arrange
        var seriesId = CreateTmdbId();
        var name = _fixture.CreateTitle();
        var imdbId = "tt" + _fixture.CreateAppleId();
        var tvdbId = _fixture.CreateAppleId();
        _handler.Enqueue(HttpStatusCode.OK, DetailsJson(seriesId, name, imdbId, tvdbId, year: DomainTestFixture.UtcToday.Year));
        var sut = _mocker.CreateInstance<TmdbClient>();

        // Act
        var title = await sut.GetTvSeriesAsync(seriesId);

        // Assert
        title.Should().NotBeNull();
        title!.ImdbId.Should().Be(imdbId);
        title.TvdbId.Should().Be(tvdbId);
        title.ImdbUrl.Should().Be(new Uri($"https://www.imdb.com/title/{imdbId}/"));
        title.TvdbUrl.Should().Be(new Uri($"https://www.thetvdb.com/dereferrer/series/{tvdbId}"));
        title.Canonical!.Source.Should().Be(CatalogueCanonicalSource.Imdb);
        title.Canonical.Id.Should().Be(imdbId);
        title.Canonical.Url.Should().Be(title.ImdbUrl);
    }

    [Fact(DisplayName =
        "When a TMDB episode has no IMDb title id, the canonical id is the TheTVDB episode id.")]
    public async Task tvdb_id_is_canonical_when_imdb_is_missing()
    {
        // Arrange
        var seriesId = CreateTmdbId();
        var season = CreateTmdbId() % 20 + 1;
        var episode = CreateTmdbId() % 40 + 1;
        var episodeId = CreateTmdbId();
        var name = _fixture.CreateTitle();
        var tvdbId = _fixture.CreateAppleId();
        _handler.Enqueue(HttpStatusCode.OK, EpisodeJson(episodeId, name, season, episode, imdbId: null, tvdbId));
        var sut = _mocker.CreateInstance<TmdbClient>();

        // Act
        var title = await sut.GetTvEpisodeAsync(seriesId, season, episode);

        // Assert
        title.Should().NotBeNull();
        title!.ImdbId.Should().BeNull();
        title.ImdbUrl.Should().BeNull();
        title.TvdbId.Should().Be(tvdbId);
        title.Canonical!.Source.Should().Be(CatalogueCanonicalSource.Tvdb);
        title.Canonical.Id.Should().Be(tvdbId.ToString());
        title.Canonical.Url.Should().Be(new Uri($"https://www.thetvdb.com/dereferrer/episode/{tvdbId}"));
        _handler.Requests.Should().ContainSingle();
        _handler.Requests[0].RequestUri!.AbsolutePath.Should().Be(
            $"/3/tv/{seriesId}/season/{season}/episode/{episode}");
    }

    [Fact(DisplayName =
        "A TMDB movie with an IMDb title id uses the IMDb title page as its canonical id.")]
    public async Task movie_uses_imdb_title_page()
    {
        // Arrange
        var movieId = CreateTmdbId();
        var name = _fixture.CreateTitle();
        var imdbId = "tt" + _fixture.CreateAppleId();
        var tvdbId = _fixture.CreateAppleId();
        var year = DomainTestFixture.UtcToday.Year;
        _handler.Enqueue(HttpStatusCode.OK, DetailsJson(movieId, name, imdbId, tvdbId, year, movie: true));
        var sut = _mocker.CreateInstance<TmdbClient>();

        // Act
        var title = await sut.GetMovieAsync(movieId);

        // Assert
        title.Should().NotBeNull();
        title!.Kind.Should().Be(TmdbTitleKind.Movie);
        title.Year.Should().Be(year);
        title.Canonical!.Source.Should().Be(CatalogueCanonicalSource.Imdb);
        title.Canonical.Url.Should().Be(new Uri($"https://www.imdb.com/title/{imdbId}/"));
        title.TvdbUrl.Should().BeNull();
    }

    [Fact(DisplayName =
        "A movie with no IMDb id has a null canonical and a null TvdbUrl, because a film must not get a TheTVDB page.")]
    public async Task movie_without_imdb_has_null_canonical_and_null_tvdb_url()
    {
        // Arrange
        var movieId = CreateTmdbId();
        var name = _fixture.CreateTitle();
        var tvdbId = _fixture.CreateAppleId();
        _handler.Enqueue(HttpStatusCode.OK, DetailsJson(movieId, name, imdbId: null, tvdbId, year: DomainTestFixture.UtcToday.Year, movie: true));
        var sut = _mocker.CreateInstance<TmdbClient>();

        // Act
        var title = await sut.GetMovieAsync(movieId);

        // Assert
        title.Should().NotBeNull();
        title!.Kind.Should().Be(TmdbTitleKind.Movie);
        title.ImdbId.Should().BeNull();
        title.ImdbUrl.Should().BeNull();
        title.Canonical.Should().BeNull();
        title.TvdbUrl.Should().BeNull();
    }

    [Fact(DisplayName =
        "A successful TMDB call sends the read access token on Authorization Bearer and does not put the key on the request URI.")]
    public async Task successful_call_sends_bearer_token_and_omits_key_from_uri()
    {
        // Arrange
        var seriesId = CreateTmdbId();
        var name = _fixture.CreateTitle();
        var imdbId = "tt" + _fixture.CreateAppleId();
        _options.ApiKey = string.Join('.', _fixture.CreateYouTubeId(), _fixture.CreateYouTubeId(), _fixture.CreateYouTubeId());
        _handler.Enqueue(HttpStatusCode.OK, DetailsJson(seriesId, name, imdbId, tvdbId: null, year: DomainTestFixture.UtcToday.Year));
        var sut = _mocker.CreateInstance<TmdbClient>();

        // Act
        var title = await sut.GetTvSeriesAsync(seriesId);

        // Assert
        title.Should().NotBeNull();
        _handler.Requests.Should().ContainSingle();
        var request = _handler.Requests[0];
        request.Headers.Authorization.Should().NotBeNull();
        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        request.Headers.Authorization.Parameter.Should().Be(_options.ApiKey);
        request.RequestUri.Should().NotBeNull();
        request.RequestUri!.OriginalString.Should().NotContain(_options.ApiKey);
        request.RequestUri.Query.Should().NotContain("api_key");
    }

    [Fact(DisplayName =
        "A TMDB v3 API key is sent as the api_key query parameter and not as a bearer token, because that key is not a read-access token.")]
    public async Task v3_api_key_is_a_query_parameter()
    {
        // Arrange
        var apiKey = Convert.ToHexString(BitConverter.GetBytes(_fixture.CreateAppleId()))
            .PadRight(32, 'a')[..32]
            .ToLowerInvariant();
        _options.ApiKey = apiKey;
        var name = _fixture.CreateTitle();
        _handler.Enqueue(HttpStatusCode.OK, SearchJson(CreateTmdbId(), name, DomainTestFixture.UtcToday.Year));
        var sut = _mocker.CreateInstance<TmdbClient>();

        // Act
        var hits = await sut.SearchTvAsync(name);

        // Assert
        hits.Should().ContainSingle();
        _handler.Requests.Should().ContainSingle();
        var request = _handler.Requests[0];
        request.Headers.Authorization.Should().BeNull();
        request.RequestUri.Should().NotBeNull();
        request.RequestUri!.Query.Should().Contain("api_key=" + apiKey);
    }

    [Fact(DisplayName =
        "A TMDB movie search maps the title and release year, and does not invent a canonical id.")]
    public async Task movie_search_maps_title_and_year()
    {
        // Arrange
        var movieId = CreateTmdbId();
        var name = _fixture.CreateTitle();
        var year = DomainTestFixture.UtcToday.Year;
        _handler.Enqueue(HttpStatusCode.OK, SearchJson(movieId, name, year));
        var sut = _mocker.CreateInstance<TmdbClient>();

        // Act
        var hits = await sut.SearchMoviesAsync(name);

        // Assert
        hits.Should().ContainSingle();
        hits[0].Id.Should().Be(movieId);
        hits[0].Name.Should().Be(name);
        hits[0].Kind.Should().Be(TmdbTitleKind.Movie);
        hits[0].Year.Should().Be(year);
    }

    [Fact(DisplayName =
        "A TMDB movie search hit keeps the original-language title beside the English title, because a curator may " +
        "search by the original title (issue #1421: \"Faderhuset\" is listed as \"Father's House\").")]
    public async Task movie_search_hit_keeps_original_title()
    {
        // Arrange
        const string listed = "Father's House";
        const string original = "Faderhuset";
        var movieId = CreateTmdbId();
        var payload = new JsonObject
        {
            ["results"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = movieId,
                    ["title"] = listed,
                    ["original_title"] = original,
                    ["release_date"] = $"{DomainTestFixture.UtcToday.Year:0000}-01-01"
                }
            }
        };
        _handler.Enqueue(HttpStatusCode.OK, payload.ToJsonString());
        var sut = _mocker.CreateInstance<TmdbClient>();

        // Act
        var hits = await sut.SearchMoviesAsync(original);

        // Assert
        hits.Should().ContainSingle();
        hits[0].Name.Should().Be(listed);
        hits[0].OriginalName.Should().Be(original);
    }

    [Fact(DisplayName =
        "A TMDB search hit whose original title is the same as its listed title carries no separate original title.")]
    public async Task same_original_title_is_not_repeated()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var payload = new JsonObject
        {
            ["results"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = CreateTmdbId(),
                    ["name"] = name,
                    ["original_name"] = name
                }
            }
        };
        _handler.Enqueue(HttpStatusCode.OK, payload.ToJsonString());
        var sut = _mocker.CreateInstance<TmdbClient>();

        // Act
        var hits = await sut.SearchTvAsync(name);

        // Assert
        hits.Should().ContainSingle().Which.OriginalName.Should().BeNull();
    }

    [Fact(DisplayName =
        "Finding a movie by IMDb title id asks TMDB find with external_source=imdb_id and returns that movie, " +
        "so an IMDb-only match can still store the TMDB id.")]
    public async Task find_movie_by_imdb_id_returns_the_tmdb_movie()
    {
        // Arrange
        var movieId = CreateTmdbId();
        var imdbId = "tt" + _fixture.CreateAppleId();
        var name = _fixture.CreateTitle();
        var original = _fixture.CreateTitleNotContainedIn(name);
        var year = DomainTestFixture.UtcToday.Year;
        var payload = new JsonObject
        {
            ["movie_results"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = movieId,
                    ["title"] = name,
                    ["original_title"] = original,
                    ["release_date"] = $"{year:0000}-01-01"
                }
            },
            ["tv_results"] = new JsonArray()
        };
        _handler.Enqueue(HttpStatusCode.OK, payload.ToJsonString());
        var sut = _mocker.CreateInstance<TmdbClient>();

        // Act
        var hit = await sut.FindMovieByImdbIdAsync(imdbId);

        // Assert
        hit.Should().NotBeNull();
        hit!.Id.Should().Be(movieId);
        hit.Name.Should().Be(name);
        hit.OriginalName.Should().Be(original);
        hit.Kind.Should().Be(TmdbTitleKind.Movie);
        hit.Year.Should().Be(year);
        _handler.Requests.Should().ContainSingle();
        _handler.Requests[0].RequestUri!.AbsolutePath.Should().Be($"/3/find/{imdbId}");
        _handler.Requests[0].RequestUri!.Query.Should().Contain("external_source=imdb_id");
    }

    [Fact(DisplayName =
        "Finding a movie by IMDb title id returns nothing when TMDB has no movie for it.")]
    public async Task find_movie_by_imdb_id_without_movie_returns_null()
    {
        // Arrange
        _handler.Enqueue(HttpStatusCode.OK, new JsonObject { ["movie_results"] = new JsonArray() }.ToJsonString());
        var sut = _mocker.CreateInstance<TmdbClient>();

        // Act
        var hit = await sut.FindMovieByImdbIdAsync("tt" + _fixture.CreateAppleId());

        // Assert
        hit.Should().BeNull();
    }

    [Fact(DisplayName =
        "Finding a movie by something that is not an IMDb title id does not call TMDB.")]
    public async Task find_movie_by_invalid_imdb_id_does_not_call()
    {
        // Arrange
        var sut = _mocker.CreateInstance<TmdbClient>();

        // Act
        var hit = await sut.FindMovieByImdbIdAsync(_fixture.CreateTitle());

        // Assert
        hit.Should().BeNull();
        _handler.Requests.Should().BeEmpty();
    }

    [Fact(DisplayName = "TMDB calls are refused when the API key is unset.")]
    public async Task missing_api_key_does_not_call()
    {
        // Arrange
        _options.ApiKey = "";
        var sut = _mocker.CreateInstance<TmdbClient>();

        // Act
        var act = () => sut.SearchTvAsync(_fixture.CreateTitle());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ApiKey*");
        _handler.Requests.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a TMDB call with a v3 API key fails, the error log and the exception name the path and status but never " +
        "the key or the api_key parameter, because the key rides in the query string and must not leak into logs.")]
    public async Task failed_call_never_logs_the_v3_key()
    {
        // Arrange
        _handler.Enqueue(HttpStatusCode.InternalServerError, "{}");
        var sut = _mocker.CreateInstance<TmdbClient>();

        // Act
        var act = () => sut.SearchTvAsync(_fixture.CreateTitle());

        // Assert
        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Message.Should().NotContain(_options.ApiKey).And.NotContain("api_key");
        _handler.Requests.Single().RequestUri!.OriginalString.Should().Contain("api_key=");
        var logged = _mocker.GetMock<ILogger<TmdbClient>>().Invocations
            .Where(invocation => invocation.Method.Name == nameof(ILogger.Log))
            .Select(invocation => invocation.Arguments[2]?.ToString() ?? string.Empty)
            .ToList();
        logged.Should().NotBeEmpty();
        logged.Should().OnlyContain(message => !message.Contains(_options.ApiKey) && !message.Contains("api_key"));
    }

    [Fact(DisplayName =
        "Movie metadata reads the overview, runtime, release date and backdrop, and lists every other title (the TMDB " +
        "title, original, alternative and translated) once (RPP #1430, #1431).")]
    public async Task movie_metadata_reads_details_and_other_titles()
    {
        // Arrange
        var movieId = CreateTmdbId();
        var payload = new JsonObject
        {
            ["id"] = movieId,
            ["title"] = "Father's House",
            ["original_title"] = "Faderhuset",
            ["overview"] = " A documentary. ",
            ["runtime"] = 88,
            ["release_date"] = "2026-10-19",
            ["poster_path"] = "/poster.jpg",
            ["backdrop_path"] = "/backdrop.jpg",
            ["alternative_titles"] = new JsonObject
            {
                ["titles"] = new JsonArray(
                    new JsonObject { ["title"] = "faderhuset" },
                    new JsonObject { ["title"] = "Fathers House" },
                    new JsonObject { ["title"] = " " })
            },
            ["translations"] = new JsonObject
            {
                ["translations"] = new JsonArray(
                    new JsonObject { ["data"] = new JsonObject { ["title"] = "Father's House" } },
                    new JsonObject { ["data"] = new JsonObject { ["title"] = "La casa del padre" } },
                    new JsonObject { ["data"] = new JsonObject { ["title"] = "" } })
            }
        };
        _handler.Enqueue(HttpStatusCode.OK, payload.ToJsonString());
        var sut = _mocker.CreateInstance<TmdbClient>();

        // Act
        var metadata = await sut.GetMovieMetadataAsync(movieId);

        // Assert
        metadata.Should().NotBeNull();
        metadata!.Id.Should().Be(movieId);
        metadata.Title.Should().Be("Father's House");
        metadata.Overview.Should().Be("A documentary.");
        metadata.RuntimeMinutes.Should().Be(88);
        metadata.ReleaseDate.Should().Be(new DateOnly(2026, 10, 19));
        metadata.Image.Should().Be(new Uri("https://image.tmdb.org/t/p/w1280/backdrop.jpg"));
        metadata.OtherTitles.Should().Equal("Father's House", "Faderhuset", "Fathers House", "La casa del padre");
        _handler.Requests.Single().RequestUri!.OriginalString
            .Should().Contain($"movie/{movieId}?append_to_response=alternative_titles,translations");
    }

    [Fact(DisplayName =
        "TMDB's listed title is stored as an alternative title when it differs from the film's name, so a search for " +
        "Father's House finds Faderhuset (RPP #1431).")]
    public async Task movie_metadata_fills_the_tmdb_title_as_an_alternative_when_it_differs_from_the_film_name()
    {
        // Arrange
        var movieId = CreateTmdbId();
        var payload = new JsonObject
        {
            ["id"] = movieId,
            ["title"] = "Father's House",
            ["original_title"] = "Faderhuset",
            ["overview"] = "A documentary.",
            ["runtime"] = 90,
            ["release_date"] = "2026-11-19",
            ["alternative_titles"] = new JsonObject
            {
                ["titles"] = new JsonArray(new JsonObject { ["title"] = "Faderhuset" })
            },
            ["translations"] = new JsonObject
            {
                ["translations"] = new JsonArray(
                    new JsonObject { ["data"] = new JsonObject { ["title"] = "Father's House" } })
            }
        };
        _handler.Enqueue(HttpStatusCode.OK, payload.ToJsonString());
        var sut = _mocker.CreateInstance<TmdbClient>();
        var film = new Film("Faderhuset") { Description = string.Empty };

        // Act
        var metadata = await sut.GetMovieMetadataAsync(movieId);
        IdentityMetadataFill.Fill(film, metadata!);

        // Assert
        film.AlternativeTitles.Should().Contain("Father's House");
    }

    [Fact(DisplayName =
        "Movie metadata uses the poster when TMDB has no backdrop, and leaves a zero runtime and a blank date unset.")]
    public async Task movie_metadata_falls_back_to_poster()
    {
        // Arrange
        var movieId = CreateTmdbId();
        var listed = _fixture.CreateTitle();
        var payload = new JsonObject
        {
            ["id"] = movieId,
            ["title"] = listed,
            ["runtime"] = 0,
            ["release_date"] = "",
            ["poster_path"] = "/poster.jpg"
        };
        _handler.Enqueue(HttpStatusCode.OK, payload.ToJsonString());
        var sut = _mocker.CreateInstance<TmdbClient>();

        // Act
        var metadata = await sut.GetMovieMetadataAsync(movieId);

        // Assert
        metadata!.Image.Should().Be(new Uri("https://image.tmdb.org/t/p/w780/poster.jpg"));
        metadata.RuntimeMinutes.Should().BeNull();
        metadata.ReleaseDate.Should().BeNull();
        metadata.OtherTitles.Should().Equal(listed);
    }

    [Fact(DisplayName =
        "TV episode metadata reads the name, overview, runtime, air date and still from the season/episode path.")]
    public async Task tv_episode_metadata_reads_details()
    {
        // Arrange
        var seriesId = CreateTmdbId();
        var episodeId = CreateTmdbId();
        var payload = new JsonObject
        {
            ["id"] = episodeId,
            ["name"] = "Pilot",
            ["overview"] = "It begins.",
            ["runtime"] = 47,
            ["air_date"] = "2019-02-03",
            ["still_path"] = "/still.jpg"
        };
        _handler.Enqueue(HttpStatusCode.OK, payload.ToJsonString());
        var sut = _mocker.CreateInstance<TmdbClient>();

        // Act
        var metadata = await sut.GetTvEpisodeMetadataAsync(seriesId, 1, 1);

        // Assert
        metadata.Should().Be(new TmdbEpisodeMetadata(
            episodeId, "Pilot", "It begins.", 47, new DateOnly(2019, 2, 3),
            new Uri("https://image.tmdb.org/t/p/w780/still.jpg")));
        _handler.Requests.Single().RequestUri!.AbsolutePath.Should().Be($"/3/tv/{seriesId}/season/1/episode/1");
    }

    [Fact(DisplayName =
        "Finding a TV episode by IMDb title id returns its series id, season and episode number from TMDB find.")]
    public async Task find_tv_episode_by_imdb_id()
    {
        // Arrange
        var imdbId = "tt" + _fixture.CreateAppleId();
        var showId = CreateTmdbId();
        var payload = new JsonObject
        {
            ["movie_results"] = new JsonArray(),
            ["tv_episode_results"] = new JsonArray(new JsonObject
            {
                ["id"] = CreateTmdbId(),
                ["show_id"] = showId,
                ["season_number"] = 3,
                ["episode_number"] = 7
            })
        };
        _handler.Enqueue(HttpStatusCode.OK, payload.ToJsonString());
        var sut = _mocker.CreateInstance<TmdbClient>();

        // Act
        var reference = await sut.FindTvEpisodeByImdbIdAsync(imdbId);

        // Assert
        reference.Should().Be(new TmdbEpisodeReference(showId, 3, 7));
    }

    private int CreateTmdbId()
    {
        var value = (int)(_fixture.CreateAppleId() % 1_000_000_000L);
        return value == 0 ? 1 : value;
    }

    private static string DetailsJson(int id, string name, string? imdbId, long? tvdbId, int year, bool movie = false)
    {
        var payload = new JsonObject
        {
            ["id"] = id,
            ["external_ids"] = ExternalIds(imdbId, tvdbId)
        };
        if (movie)
        {
            payload["title"] = name;
            payload["release_date"] = $"{year:0000}-01-01";
            payload["imdb_id"] = imdbId;
        }
        else
        {
            payload["name"] = name;
            payload["first_air_date"] = $"{year:0000}-01-01";
        }

        return payload.ToJsonString();
    }

    private static string EpisodeJson(int id, string name, int season, int episode, string? imdbId, long tvdbId)
    {
        var payload = new JsonObject
        {
            ["id"] = id,
            ["name"] = name,
            ["season_number"] = season,
            ["episode_number"] = episode,
            ["air_date"] = $"{DomainTestFixture.UtcToday.Year:0000}-01-01",
            ["external_ids"] = ExternalIds(imdbId, tvdbId)
        };
        return payload.ToJsonString();
    }

    private static string SearchJson(int id, string name, int year)
    {
        var payload = new JsonObject
        {
            ["results"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = id,
                    ["title"] = name,
                    ["release_date"] = $"{year:0000}-01-01"
                }
            }
        };
        return payload.ToJsonString();
    }

    private static JsonObject ExternalIds(string? imdbId, long? tvdbId)
    {
        var ids = new JsonObject();
        if (imdbId is not null)
        {
            ids["imdb_id"] = imdbId;
        }

        if (tvdbId is not null)
        {
            ids["tvdb_id"] = tvdbId;
        }

        return ids;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public List<HttpRequestMessage> Requests { get; } = [];

        public StubHandler Enqueue(HttpStatusCode status, string json)
        {
            _responses.Enqueue(new HttpResponseMessage(status)
            {
                Content = new StringContent(json)
            });
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
