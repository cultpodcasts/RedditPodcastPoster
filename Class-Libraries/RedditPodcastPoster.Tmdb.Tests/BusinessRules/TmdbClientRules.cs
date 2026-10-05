using System.Net;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Tmdb.Clients;
using RedditPodcastPoster.Tmdb.Configuration;
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
        var year = DomainTestFixture.UtcToday.Year;
        _handler.Enqueue(HttpStatusCode.OK, DetailsJson(movieId, name, imdbId, tvdbId: null, year, movie: true));
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
