using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Catalogue.Extensions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.TheTvdb.Clients;
using RedditPodcastPoster.TheTvdb.Models;
using RedditPodcastPoster.Tmdb.Clients;
using RedditPodcastPoster.Tmdb.Models;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules;

public class CatalogueAuthorityLookupRules
{
    private readonly AutoMocker _mocker = new();
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A film collects its IMDb id and TMDB id from TMDB and stores no TheTVDB id, because TheTVDB is for TV shows and episodes.")]
    public async Task film_collects_imdb_and_tmdb_ids_and_drops_thetvdb()
    {
        // Arrange
        var tmdbId = CreateTmdbId();
        var imdbId = "tt" + _fixture.CreateAppleId().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var droppedTvdbId = _fixture.CreateAppleId();
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.GetMovieAsync(tmdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbTitle(
                tmdbId,
                _fixture.CreateTitle(),
                TmdbTitleKind.Movie,
                Year: null,
                SeasonNumber: null,
                EpisodeNumber: null,
                imdbId,
                droppedTvdbId,
                ImdbUrl: null,
                TvdbUrl: null,
                Canonical: null));
        var sut = _mocker.CreateInstance<CatalogueAuthorityLookup>();

        // Act
        var ids = await sut.CollectFilmAsync(tmdbId);
        var film = new Film(_fixture.CreateTitle());
        ids.Apply(film);

        // Assert
        ids.ImdbId.Should().Be(imdbId);
        ids.TmdbId.Should().Be(tmdbId);
        ids.TvdbId.Should().BeNull();
        ids.Tvdb.Should().BeNull();
        film.ImdbId.Should().Be(imdbId);
        film.TmdbId.Should().Be(tmdbId);
        film.Imdb.Should().Be(new Uri($"https://www.imdb.com/title/{imdbId}/"));
        _mocker.GetMock<ITheTvdbClient>().VerifyNoOtherCalls();
    }

    [Fact(DisplayName =
        "A TV show collects the IMDb id, TMDB series id, and TheTVDB series id by calling both authorities.")]
    public async Task tv_show_collects_imdb_tmdb_and_thetvdb_ids()
    {
        // Arrange
        var tmdbId = CreateTmdbId();
        var tvdbId = _fixture.CreateAppleId();
        var imdbId = "tt" + _fixture.CreateAppleId().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var slug = _fixture.CreateYouTubeId();
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.GetTvSeriesAsync(tmdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbTitle(
                tmdbId,
                _fixture.CreateTitle(),
                TmdbTitleKind.TvSeries,
                Year: null,
                SeasonNumber: null,
                EpisodeNumber: null,
                imdbId,
                tvdbId,
                ImdbUrl: null,
                TvdbUrl: null,
                Canonical: null));
        _mocker.GetMock<ITheTvdbClient>()
            .Setup(client => client.GetSeriesAsync(tvdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TheTvdbSeries(
                tvdbId,
                _fixture.CreateTitle(),
                Year: null,
                Country: null,
                slug,
                ImdbTitleId: null,
                new Uri($"https://www.thetvdb.com/series/{slug}"),
                ImdbUrl: null));
        var sut = _mocker.CreateInstance<CatalogueAuthorityLookup>();

        // Act
        var ids = await sut.CollectTvShowAsync(tmdbId, tvdbId: null);

        // Assert
        ids.ImdbId.Should().Be(imdbId);
        ids.TmdbId.Should().Be(tmdbId);
        ids.TvdbId.Should().Be(tvdbId);
        ids.Tvdb.Should().Be(new Uri($"https://www.thetvdb.com/series/{slug}"));
        _mocker.GetMock<ITheTvdbClient>().Verify(
            client => client.GetSeriesAsync(tvdbId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "A TV show with no IMDb id on TMDB keeps the IMDb id returned by TheTVDB.")]
    public async Task tv_show_uses_thetvdb_imdb_id_when_tmdb_has_none()
    {
        // Arrange
        var tvdbId = _fixture.CreateAppleId();
        var imdbId = "tt" + _fixture.CreateAppleId().ToString(System.Globalization.CultureInfo.InvariantCulture);
        _mocker.GetMock<ITheTvdbClient>()
            .Setup(client => client.GetSeriesAsync(tvdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TheTvdbSeries(
                tvdbId,
                _fixture.CreateTitle(),
                Year: null,
                Country: null,
                Slug: null,
                imdbId,
                CanonicalUrl: null,
                ImdbUrl: null));
        var sut = _mocker.CreateInstance<CatalogueAuthorityLookup>();

        // Act
        var ids = await sut.CollectTvShowAsync(tmdbId: null, tvdbId);

        // Assert
        ids.ImdbId.Should().Be(imdbId);
        ids.TmdbId.Should().BeNull();
        ids.TvdbId.Should().Be(tvdbId);
        ids.Imdb.Should().Be(new Uri($"https://www.imdb.com/title/{imdbId}/"));
        _mocker.GetMock<ITmdbClient>().Verify(
            client => client.GetTvSeriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "A TV episode collects the episode IMDb id, TMDB episode id, and TheTVDB episode id from both authorities.")]
    public async Task tv_episode_collects_imdb_tmdb_and_thetvdb_ids()
    {
        // Arrange
        var seriesId = CreateTmdbId();
        var season = CreateTmdbId() % 20 + 1;
        var number = CreateTmdbId() % 40 + 1;
        var episodeTmdbId = CreateTmdbId();
        var tvdbEpisodeId = _fixture.CreateAppleId();
        var imdbId = "tt" + _fixture.CreateAppleId().ToString(System.Globalization.CultureInfo.InvariantCulture);
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.GetTvEpisodeAsync(seriesId, season, number, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbTitle(
                episodeTmdbId,
                _fixture.CreateTitle(),
                TmdbTitleKind.TvEpisode,
                Year: null,
                season,
                number,
                imdbId,
                tvdbEpisodeId,
                ImdbUrl: null,
                TvdbUrl: null,
                Canonical: null));
        _mocker.GetMock<ITheTvdbClient>()
            .Setup(client => client.GetEpisodeAsync(tvdbEpisodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TheTvdbEpisode(
                tvdbEpisodeId,
                SeriesId: _fixture.CreateAppleId(),
                _fixture.CreateTitle(),
                season,
                number,
                Year: null,
                ImdbTitleId: null,
                CanonicalUrl: null,
                ImdbUrl: null));
        var sut = _mocker.CreateInstance<CatalogueAuthorityLookup>();

        // Act
        var ids = await sut.CollectTvShowEpisodeAsync(seriesId, season, number, tvdbEpisodeId: null);

        // Assert
        ids.ImdbId.Should().Be(imdbId);
        ids.TmdbId.Should().Be(episodeTmdbId);
        ids.TvdbId.Should().Be(tvdbEpisodeId);
        ids.Tvdb.Should().Be(new Uri($"https://www.thetvdb.com/dereferrer/episode/{tvdbEpisodeId}"));
    }

    [Fact(DisplayName =
        "Catalogue registration includes the TMDB client, the TheTVDB client, and the authority lookup that uses both.")]
    public void catalogue_services_register_both_authority_clients()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();

        // Act
        services.AddCatalogueServices();

        // Assert
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(ICatalogueAuthorityLookup));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(ITmdbClient));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(ITheTvdbClient));
    }

    private int CreateTmdbId()
    {
        var value = (int)(_fixture.CreateAppleId() % 1_000_000_000L);
        return value == 0 ? 1 : value;
    }
}
