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
using RedditPodcastPoster.Models.TvShows;
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
                TmdbSeriesId: null,
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
                TmdbSeriesId: null,
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
                TmdbSeriesId: null,
                TmdbEpisodeId: null,
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

    [Fact(DisplayName =
        "The authority lookup is scoped and the TheTVDB login session is a singleton, so the transient client does not pin the handler or the login token.")]
    public void authority_lookup_is_scoped_and_thetvdb_login_session_is_singleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();

        // Act
        services.AddCatalogueServices();

        // Assert
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(ICatalogueAuthorityLookup) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(ITheTvdbClient) &&
            descriptor.Lifetime == ServiceLifetime.Transient);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(TheTvdbLoginSession) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact(DisplayName =
        "A TV show known only by its TheTVDB id stores the IMDb id, the TMDB series id, and the TheTVDB id, because the series record carries the TMDB series id.")]
    public async Task tvdb_only_show_stores_imdb_tmdb_and_thetvdb_ids()
    {
        // Arrange
        var tvdbId = _fixture.CreateAppleId();
        var tmdbSeriesId = CreateTmdbId();
        var imdbId = "tt" + _fixture.CreateAppleId().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var slug = _fixture.CreateYouTubeId();
        var canonical = new Uri($"https://www.thetvdb.com/series/{slug}");
        _mocker.GetMock<ITheTvdbClient>()
            .Setup(client => client.GetSeriesAsync(tvdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TheTvdbSeries(
                tvdbId,
                _fixture.CreateTitle(),
                Year: null,
                Country: null,
                slug,
                imdbId,
                tmdbSeriesId,
                canonical,
                ImdbUrl: null));
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.GetTvSeriesAsync(tmdbSeriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbTitle(
                tmdbSeriesId,
                _fixture.CreateTitle(),
                TmdbTitleKind.TvSeries,
                Year: null,
                SeasonNumber: null,
                EpisodeNumber: null,
                ImdbId: null,
                tvdbId,
                ImdbUrl: null,
                TvdbUrl: null,
                Canonical: null));
        var sut = _mocker.CreateInstance<CatalogueAuthorityLookup>();

        // Act
        var ids = await sut.CollectTvShowAsync(tmdbId: null, tvdbId);
        var show = new TvShow(_fixture.CreateTitle());
        ids.Apply(show);

        // Assert
        ids.ImdbId.Should().Be(imdbId);
        ids.TmdbId.Should().Be(tmdbSeriesId);
        ids.TvdbId.Should().Be(tvdbId);
        ids.Imdb.Should().Be(new Uri($"https://www.imdb.com/title/{imdbId}/"));
        ids.Tvdb.Should().Be(canonical);
        show.ImdbId.Should().Be(imdbId);
        show.TmdbId.Should().Be(tmdbSeriesId);
        show.TvdbId.Should().Be(tvdbId);
        _mocker.GetMock<ITmdbClient>().Verify(
            client => client.GetTvSeriesAsync(tmdbSeriesId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "A TV episode known only by its TheTVDB id stores the IMDb id, the TMDB episode id, and the TheTVDB id, because the parent series supplies the TMDB series id.")]
    public async Task tvdb_only_episode_stores_imdb_tmdb_and_thetvdb_ids()
    {
        // Arrange
        var tvdbEpisodeId = _fixture.CreateAppleId();
        var parentTvdbId = _fixture.CreateAppleId();
        var seriesTmdbId = CreateTmdbId();
        var episodeTmdbId = CreateDistinctTmdbId(seriesTmdbId);
        var season = CreateTmdbId() % 20 + 1;
        var number = CreateTmdbId() % 40 + 1;
        var imdbId = "tt" + _fixture.CreateAppleId().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var slug = _fixture.CreateYouTubeId();
        var canonical = new Uri($"https://www.thetvdb.com/series/{slug}/episodes/{tvdbEpisodeId}");
        _mocker.GetMock<ITheTvdbClient>()
            .Setup(client => client.GetEpisodeAsync(tvdbEpisodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TheTvdbEpisode(
                tvdbEpisodeId,
                parentTvdbId,
                _fixture.CreateTitle(),
                season,
                number,
                Year: null,
                imdbId,
                TmdbSeriesId: null,
                episodeTmdbId,
                canonical,
                ImdbUrl: null));
        _mocker.GetMock<ITheTvdbClient>()
            .Setup(client => client.GetSeriesAsync(parentTvdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TheTvdbSeries(
                parentTvdbId,
                _fixture.CreateTitle(),
                Year: null,
                Country: null,
                slug,
                ImdbTitleId: null,
                seriesTmdbId,
                CanonicalUrl: null,
                ImdbUrl: null));
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.GetTvEpisodeAsync(seriesTmdbId, season, number, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbTitle(
                episodeTmdbId,
                _fixture.CreateTitle(),
                TmdbTitleKind.TvEpisode,
                Year: null,
                season,
                number,
                ImdbId: null,
                tvdbEpisodeId,
                ImdbUrl: null,
                TvdbUrl: null,
                Canonical: null));
        var sut = _mocker.CreateInstance<CatalogueAuthorityLookup>();

        // Act
        var ids = await sut.CollectTvShowEpisodeAsync(
            tmdbSeriesId: null,
            seasonNumber: null,
            episodeNumber: null,
            tvdbEpisodeId);
        var episode = new TvShowEpisode(_fixture.CreateTitle());
        ids.Apply(episode);

        // Assert
        ids.ImdbId.Should().Be(imdbId);
        ids.TmdbId.Should().Be(episodeTmdbId);
        ids.TvdbId.Should().Be(tvdbEpisodeId);
        ids.Tvdb.Should().Be(canonical);
        episode.ImdbId.Should().Be(imdbId);
        episode.TmdbId.Should().Be(episodeTmdbId);
        episode.TvdbId.Should().Be(tvdbEpisodeId);
        _mocker.GetMock<ITmdbClient>().Verify(
            client => client.GetTvEpisodeAsync(seriesTmdbId, season, number, It.IsAny<CancellationToken>()),
            Times.Once);
        _mocker.GetMock<ITmdbClient>().Verify(
            client => client.GetTvSeriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mocker.GetMock<ITmdbClient>().Verify(
            client => client.GetTvEpisodeAsync(episodeTmdbId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "A TheMovieDB id on a TheTVDB episode row is not stored when it differs from the TMDB episode, and it is not used as the series id.")]
    public async Task episode_row_tmdb_id_is_not_a_series_id_when_it_differs_from_the_payload()
    {
        // Arrange
        var tvdbEpisodeId = _fixture.CreateAppleId();
        var parentTvdbId = _fixture.CreateAppleId();
        var seriesTmdbId = CreateTmdbId();
        var payloadTmdbId = CreateDistinctTmdbId(seriesTmdbId);
        var episodeRowTmdbId = CreateDistinctTmdbId(seriesTmdbId, payloadTmdbId);
        var season = CreateTmdbId() % 20 + 1;
        var number = CreateTmdbId() % 40 + 1;
        _mocker.GetMock<ITheTvdbClient>()
            .Setup(client => client.GetEpisodeAsync(tvdbEpisodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TheTvdbEpisode(
                tvdbEpisodeId,
                parentTvdbId,
                _fixture.CreateTitle(),
                season,
                number,
                Year: null,
                ImdbTitleId: null,
                TmdbSeriesId: episodeRowTmdbId,
                episodeRowTmdbId,
                CanonicalUrl: null,
                ImdbUrl: null));
        _mocker.GetMock<ITheTvdbClient>()
            .Setup(client => client.GetSeriesAsync(parentTvdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TheTvdbSeries(
                parentTvdbId,
                _fixture.CreateTitle(),
                Year: null,
                Country: null,
                Slug: null,
                ImdbTitleId: null,
                seriesTmdbId,
                CanonicalUrl: null,
                ImdbUrl: null));
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.GetTvEpisodeAsync(seriesTmdbId, season, number, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbTitle(
                payloadTmdbId,
                _fixture.CreateTitle(),
                TmdbTitleKind.TvEpisode,
                Year: null,
                season,
                number,
                ImdbId: null,
                tvdbEpisodeId,
                ImdbUrl: null,
                TvdbUrl: null,
                Canonical: null));
        var sut = _mocker.CreateInstance<CatalogueAuthorityLookup>();

        // Act
        var ids = await sut.CollectTvShowEpisodeAsync(
            tmdbSeriesId: null,
            seasonNumber: null,
            episodeNumber: null,
            tvdbEpisodeId);

        // Assert
        ids.TmdbId.Should().Be(payloadTmdbId);
        ids.TmdbId.Should().NotBe(episodeRowTmdbId);
        _mocker.GetMock<ITmdbClient>().Verify(
            client => client.GetTvSeriesAsync(episodeRowTmdbId, It.IsAny<CancellationToken>()),
            Times.Never);
        _mocker.GetMock<ITmdbClient>().Verify(
            client => client.GetTvEpisodeAsync(episodeRowTmdbId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "A TheMovieDB id on a TheTVDB episode row is not stored when TMDB returns no episode, because that id is kept only when it matches the payload.")]
    public async Task episode_row_tmdb_id_is_not_stored_when_tmdb_returns_no_episode()
    {
        // Arrange
        var tvdbEpisodeId = _fixture.CreateAppleId();
        var parentTvdbId = _fixture.CreateAppleId();
        var seriesTmdbId = CreateTmdbId();
        var episodeRowTmdbId = CreateDistinctTmdbId(seriesTmdbId);
        var season = CreateTmdbId() % 20 + 1;
        var number = CreateTmdbId() % 40 + 1;
        var imdbId = "tt" + _fixture.CreateAppleId().ToString(System.Globalization.CultureInfo.InvariantCulture);
        _mocker.GetMock<ITheTvdbClient>()
            .Setup(client => client.GetEpisodeAsync(tvdbEpisodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TheTvdbEpisode(
                tvdbEpisodeId,
                parentTvdbId,
                _fixture.CreateTitle(),
                season,
                number,
                Year: null,
                imdbId,
                TmdbSeriesId: null,
                episodeRowTmdbId,
                CanonicalUrl: null,
                ImdbUrl: null));
        _mocker.GetMock<ITheTvdbClient>()
            .Setup(client => client.GetSeriesAsync(parentTvdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TheTvdbSeries(
                parentTvdbId,
                _fixture.CreateTitle(),
                Year: null,
                Country: null,
                Slug: null,
                ImdbTitleId: null,
                seriesTmdbId,
                CanonicalUrl: null,
                ImdbUrl: null));
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.GetTvEpisodeAsync(seriesTmdbId, season, number, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TmdbTitle?)null);
        var sut = _mocker.CreateInstance<CatalogueAuthorityLookup>();

        // Act
        var ids = await sut.CollectTvShowEpisodeAsync(
            tmdbSeriesId: null,
            seasonNumber: null,
            episodeNumber: null,
            tvdbEpisodeId);

        // Assert
        ids.ImdbId.Should().Be(imdbId);
        ids.TmdbId.Should().BeNull();
        ids.TvdbId.Should().Be(tvdbEpisodeId);
        _mocker.GetMock<ITmdbClient>().Verify(
            client => client.GetTvEpisodeAsync(seriesTmdbId, season, number, It.IsAny<CancellationToken>()),
            Times.Once);
        _mocker.GetMock<ITmdbClient>().Verify(
            client => client.GetTvSeriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
}
