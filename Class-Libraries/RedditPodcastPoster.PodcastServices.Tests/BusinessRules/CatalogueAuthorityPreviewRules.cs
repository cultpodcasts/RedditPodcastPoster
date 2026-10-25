using System.Globalization;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.TheTvdb.Clients;
using RedditPodcastPoster.TheTvdb.Models;
using RedditPodcastPoster.Tmdb.Clients;
using RedditPodcastPoster.Tmdb.Models;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules;

public class CatalogueAuthorityPreviewRules
{
    private readonly AutoMocker _mocker = new();
    private readonly DomainTestFixture _fixture = new();

    public CatalogueAuthorityPreviewRules()
    {
        _mocker.GetMock<ITheTvdbClient>()
            .Setup(client => client.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<TheTvdbSearchType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    [Fact(DisplayName =
        "A film title with one TMDB hit reports the IMDb id and TMDB id and no TheTVDB id, because films do not store a TheTVDB id.")]
    public async Task one_film_hit_reports_imdb_and_tmdb_and_drops_thetvdb()
    {
        // Arrange
        var query = _fixture.CreateTitle();
        var tmdbId = CreateTmdbId();
        var imdbId = ImdbId();
        var droppedTvdbId = _fixture.CreateAppleId();
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchMoviesAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Hit(tmdbId, query, TmdbTitleKind.Movie)]);
        _mocker.GetMock<ICatalogueAuthorityLookup>()
            .Setup(lookup => lookup.CollectFilmAsync(tmdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityIds(imdbId, tmdbId, droppedTvdbId));
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewFilmAsync(query);

        // Assert
        preview.HitCount.Should().Be(1);
        preview.ImdbId.Should().Be(imdbId);
        preview.TmdbId.Should().Be(tmdbId);
        preview.TvdbId.Should().BeNull();
        preview.LookupFailed.Should().BeFalse();
        preview.HitLabels.Should().ContainSingle().Which.Should().Be(query);
    }

    [Fact(DisplayName =
        "A film title with more than one TMDB hit reports the sample names and chooses no id, because the first hit is not authority.")]
    public async Task many_film_hits_choose_no_id()
    {
        // Arrange
        var query = _fixture.CreateTitle();
        var first = _fixture.CreateTitle();
        var second = _fixture.CreateTitle();
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchMoviesAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Hit(CreateTmdbId(), first, TmdbTitleKind.Movie),
                Hit(CreateTmdbId(), second, TmdbTitleKind.Movie)
            ]);
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewFilmAsync(query);

        // Assert
        preview.HitCount.Should().Be(2);
        preview.ImdbId.Should().BeNull();
        preview.TmdbId.Should().BeNull();
        preview.TvdbId.Should().BeNull();
        preview.HitLabels.Should().Equal(first, second);
        preview.Candidates.Should().NotBeNull();
        preview.Candidates!.Should().HaveCount(2);
        preview.Candidates.Should().OnlyContain(c => c.Source == CatalogueAuthorityCandidate.TmdbSource);
        preview.Candidates.Should().OnlyContain(c =>
            c.PageUrl != null &&
            c.PageUrl.Host == "www.themoviedb.org" &&
            c.PageUrl.AbsolutePath.StartsWith("/movie/", StringComparison.Ordinal));
    }

    [Fact(DisplayName =
        "Several film TMDB hits expose a poster thumbnail and overview description on each ambiguous candidate.")]
    public async Task many_film_hits_include_thumbnail_and_description()
    {
        // Arrange
        var query = _fixture.CreateTitle();
        var first = _fixture.CreateTitle();
        var second = _fixture.CreateTitle();
        var firstId = CreateTmdbId();
        var secondId = CreateTmdbId();
        var overview = _fixture.CreateTitle();
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchMoviesAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Hit(firstId, first, TmdbTitleKind.Movie, posterPath: $"/p{firstId}.jpg", overview: overview),
                Hit(secondId, second, TmdbTitleKind.Movie, posterPath: $"/p{secondId}.jpg", overview: overview)
            ]);
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewFilmAsync(query);

        // Assert
        preview.Candidates.Should().NotBeNull();
        preview.Candidates!.Should().HaveCount(2);
        preview.Candidates.Should().OnlyContain(c =>
            c.ThumbnailUrl != null &&
            c.ThumbnailUrl.AbsoluteUri.Contains("/t/p/w185/", StringComparison.Ordinal) &&
            c.Description == overview);
    }

    [Fact(DisplayName =
        "A film title with no hit on the full name and one TMDB hit on the clause before the colon uses that clause and stores no TheTVDB id.")]
    public async Task empty_film_title_retries_the_clause_before_the_colon()
    {
        // Arrange
        var head = _fixture.CreateTitle();
        var tail = _fixture.CreateTitle();
        var query = $"{head}: {tail}";
        var tmdbId = CreateTmdbId();
        var imdbId = ImdbId();
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchMoviesAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchMoviesAsync(head, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Hit(tmdbId, head, TmdbTitleKind.Movie)]);
        _mocker.GetMock<ICatalogueAuthorityLookup>()
            .Setup(lookup => lookup.CollectFilmAsync(tmdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityIds(imdbId, tmdbId, _fixture.CreateAppleId()));
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewFilmAsync(query);

        // Assert
        preview.Query.Should().Be(head);
        preview.HitCount.Should().Be(1);
        preview.ImdbId.Should().Be(imdbId);
        preview.TmdbId.Should().Be(tmdbId);
        preview.TvdbId.Should().BeNull();
    }

    [Fact(DisplayName =
        "A TV show title with one TMDB hit reports the IMDb id, TMDB id, and TheTVDB id from the authority lookup.")]
    public async Task one_tv_hit_reports_imdb_tmdb_and_thetvdb_ids()
    {
        // Arrange
        var query = _fixture.CreateTitle();
        var tmdbId = CreateTmdbId();
        var tvdbId = _fixture.CreateAppleId();
        var imdbId = ImdbId();
        var year = DomainTestFixture.UtcToday.Year;
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchTvAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Hit(tmdbId, query, TmdbTitleKind.TvSeries, year)]);
        _mocker.GetMock<ICatalogueAuthorityLookup>()
            .Setup(lookup => lookup.CollectTvShowAsync(tmdbId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityIds(imdbId, tmdbId, tvdbId));
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewTvShowAsync(query);

        // Assert
        preview.HitCount.Should().Be(1);
        preview.ImdbId.Should().Be(imdbId);
        preview.TmdbId.Should().Be(tmdbId);
        preview.TvdbId.Should().Be(tvdbId);
        preview.HitLabels.Should().ContainSingle().Which.Should().Be($"{query} ({year.ToString(CultureInfo.InvariantCulture)})");
    }

    [Fact(DisplayName =
        "A TV show title with no TMDB hit and no TheTVDB hit reports zero hits and no ids, because neither authority matched.")]
    public async Task no_tv_hit_reports_no_ids()
    {
        // Arrange
        var query = _fixture.CreateTitle();
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchTvAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewTvShowAsync(query);

        // Assert
        preview.HitCount.Should().Be(0);
        preview.ImdbId.Should().BeNull();
        preview.TmdbId.Should().BeNull();
        preview.TvdbId.Should().BeNull();
        preview.HitLabels.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A TV show title with no TMDB hit and one TheTVDB series reports that series, because a miss on TMDB can still be found on TheTVDB.")]
    public async Task no_tmdb_hit_uses_one_thetvdb_series()
    {
        // Arrange
        var query = _fixture.CreateTitle();
        var tvdbId = _fixture.CreateAppleId();
        var tmdbId = CreateTmdbId();
        var imdbId = ImdbId();
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchTvAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mocker.GetMock<ITheTvdbClient>()
            .Setup(client => client.SearchAsync(query, TheTvdbSearchType.Series, It.IsAny<CancellationToken>()))
            .ReturnsAsync([SeriesHit(tvdbId, query)]);
        _mocker.GetMock<ICatalogueAuthorityLookup>()
            .Setup(lookup => lookup.CollectTvShowAsync(null, tvdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityIds(imdbId, tmdbId, tvdbId));
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewTvShowAsync(query);

        // Assert
        preview.Query.Should().Be(query);
        preview.HitCount.Should().Be(1);
        preview.ImdbId.Should().Be(imdbId);
        preview.TmdbId.Should().Be(tmdbId);
        preview.TvdbId.Should().Be(tvdbId);
    }

    [Fact(DisplayName =
        "A TV show with one TMDB hit and no TheTVDB id on that hit searches TheTVDB and keeps the series when there is one, because the TMDB record did not carry a TheTVDB id.")]
    public async Task one_tmdb_hit_without_tvdb_id_uses_one_thetvdb_series()
    {
        // Arrange
        var query = _fixture.CreateTitle();
        var tmdbId = CreateTmdbId();
        var tvdbId = _fixture.CreateAppleId();
        var imdbId = ImdbId();
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchTvAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Hit(tmdbId, query, TmdbTitleKind.TvSeries)]);
        _mocker.GetMock<ICatalogueAuthorityLookup>()
            .Setup(lookup => lookup.CollectTvShowAsync(tmdbId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityIds(imdbId, tmdbId, null));
        _mocker.GetMock<ITheTvdbClient>()
            .Setup(client => client.SearchAsync(query, TheTvdbSearchType.Series, It.IsAny<CancellationToken>()))
            .ReturnsAsync([SeriesHit(tvdbId, query)]);
        _mocker.GetMock<ICatalogueAuthorityLookup>()
            .Setup(lookup => lookup.CollectTvShowAsync(tmdbId, tvdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityIds(imdbId, tmdbId, tvdbId));
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewTvShowAsync(query);

        // Assert
        preview.HitCount.Should().Be(1);
        preview.ImdbId.Should().Be(imdbId);
        preview.TmdbId.Should().Be(tmdbId);
        preview.TvdbId.Should().Be(tvdbId);
    }

    [Fact(DisplayName =
        "A TV show title with no hits on the full name and one TMDB hit on the clause before the colon uses that clause, because a subtitle after a colon was hiding the show.")]
    public async Task empty_full_title_retries_the_clause_before_the_colon()
    {
        // Arrange
        var head = _fixture.CreateTitle();
        var tail = _fixture.CreateTitle();
        var query = $"{head}: {tail}";
        var tmdbId = CreateTmdbId();
        var imdbId = ImdbId();
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchTvAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchTvAsync(head, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Hit(tmdbId, head, TmdbTitleKind.TvSeries)]);
        _mocker.GetMock<ICatalogueAuthorityLookup>()
            .Setup(lookup => lookup.CollectTvShowAsync(tmdbId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityIds(imdbId, tmdbId, null));
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewTvShowAsync(query);

        // Assert
        preview.Query.Should().Be(head);
        preview.HitCount.Should().Be(1);
        preview.TmdbId.Should().Be(tmdbId);
        preview.ImdbId.Should().Be(imdbId);
        preview.TvdbId.Should().BeNull();
    }

    [Fact(DisplayName =
        "A TV show title with no TMDB hit and more than one TheTVDB series chooses no id, because the first TheTVDB hit is not authority.")]
    public async Task many_thetvdb_hits_choose_no_id()
    {
        // Arrange
        var query = _fixture.CreateTitle();
        var first = _fixture.CreateTitle();
        var second = _fixture.CreateTitle();
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchTvAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mocker.GetMock<ITheTvdbClient>()
            .Setup(client => client.SearchAsync(query, TheTvdbSearchType.Series, It.IsAny<CancellationToken>()))
            .ReturnsAsync([SeriesHit(_fixture.CreateAppleId(), first), SeriesHit(_fixture.CreateAppleId(), second)]);
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewTvShowAsync(query);

        // Assert
        preview.HitCount.Should().Be(2);
        preview.ImdbId.Should().BeNull();
        preview.TmdbId.Should().BeNull();
        preview.TvdbId.Should().BeNull();
        preview.HitLabels.Should().Equal(first, second);
    }

    [Fact(DisplayName =
        "A TV show title with one TMDB hit whose name does not contain the query, and is not contained by it, chooses no id, because a different title is not the show.")]
    public async Task one_tv_hit_with_a_different_title_chooses_no_id()
    {
        // Arrange
        var query = _fixture.CreateTitle();
        var other = _fixture.CreateTitle();
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchTvAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Hit(CreateTmdbId(), other, TmdbTitleKind.TvSeries)]);
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewTvShowAsync(query);

        // Assert
        preview.HitCount.Should().Be(1);
        preview.ImdbId.Should().BeNull();
        preview.TmdbId.Should().BeNull();
        preview.TvdbId.Should().BeNull();
        preview.HitLabels.Should().ContainSingle().Which.Should().Be(other);
    }

    [Fact(DisplayName =
        "A TV show title with no hit on the full name and one TMDB hit on the name before a parenthetical uses that name, because the parenthetical is a channel note.")]
    public async Task empty_full_title_retries_the_name_before_a_parenthetical()
    {
        // Arrange
        var show = _fixture.CreateTitle();
        var note = _fixture.CreateTitle();
        var query = $"{show} ({note})";
        var tmdbId = CreateTmdbId();
        var imdbId = ImdbId();
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchTvAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchTvAsync(show, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Hit(tmdbId, show, TmdbTitleKind.TvSeries)]);
        _mocker.GetMock<ICatalogueAuthorityLookup>()
            .Setup(lookup => lookup.CollectTvShowAsync(tmdbId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityIds(imdbId, tmdbId, null));
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewTvShowAsync(query);

        // Assert
        preview.Query.Should().Be(show);
        preview.HitCount.Should().Be(1);
        preview.ImdbId.Should().Be(imdbId);
        preview.TmdbId.Should().Be(tmdbId);
        preview.TvdbId.Should().BeNull();
    }

    [Fact(DisplayName =
        "A TV show title with no hit on the full name and one TMDB hit on the clause after a hyphen uses that clause, because the clause before the hyphen is the strand.")]
    public async Task empty_full_title_retries_the_clause_after_a_hyphen()
    {
        // Arrange
        var strand = _fixture.CreateTitle();
        var programme = _fixture.CreateTitle();
        var query = $"{strand} - {programme}";
        var tmdbId = CreateTmdbId();
        var imdbId = ImdbId();
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchTvAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchTvAsync(programme, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Hit(tmdbId, programme, TmdbTitleKind.TvSeries)]);
        _mocker.GetMock<ICatalogueAuthorityLookup>()
            .Setup(lookup => lookup.CollectTvShowAsync(tmdbId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityIds(imdbId, tmdbId, null));
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewTvShowAsync(query);

        // Assert
        preview.Query.Should().Be(programme);
        preview.HitCount.Should().Be(1);
        preview.ImdbId.Should().Be(imdbId);
        preview.TmdbId.Should().Be(tmdbId);
        preview.TvdbId.Should().BeNull();
    }

    [Fact(DisplayName =
        "A TV title with several programmes on the full name keeps that name and chooses no id, because a shorter clause must not pick one programme out of several.")]
    public async Task several_programmes_on_the_full_title_are_not_one_show()
    {
        // Arrange
        var head = _fixture.CreateTitle();
        var tail = _fixture.CreateTitle();
        var query = $"{head}: {tail}";
        var first = _fixture.CreateTitle();
        var second = _fixture.CreateTitle();
        var tmdbId = CreateTmdbId();
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchTvAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Hit(CreateTmdbId(), first, TmdbTitleKind.TvSeries),
                Hit(CreateTmdbId(), second, TmdbTitleKind.TvSeries)
            ]);
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchTvAsync(head, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Hit(tmdbId, head, TmdbTitleKind.TvSeries)]);
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewTvShowAsync(query);

        // Assert
        preview.Query.Should().Be(query);
        preview.HitCount.Should().Be(2);
        preview.Resolution.Should().Be(CatalogueAuthorityResolution.SeveralProgrammes);
        preview.ImdbId.Should().BeNull();
        preview.TmdbId.Should().BeNull();
        preview.TvdbId.Should().BeNull();
        preview.HitLabels.Should().Equal(first, second);
    }

    [Fact(DisplayName =
        "A TV title with no hit on the full name and several programmes on the name before a parenthetical keeps the full name, because the parenthetical was distinguishing the show.")]
    public async Task several_programmes_on_a_shortened_name_keep_the_full_title()
    {
        // Arrange
        var show = _fixture.CreateTitle();
        var note = _fixture.CreateTitle();
        var query = $"{show} ({note})";
        var first = _fixture.CreateTitle();
        var second = _fixture.CreateTitle();
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchTvAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchTvAsync(show, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Hit(CreateTmdbId(), first, TmdbTitleKind.TvSeries),
                Hit(CreateTmdbId(), second, TmdbTitleKind.TvSeries)
            ]);
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewTvShowAsync(query);

        // Assert
        preview.Query.Should().Be(query);
        preview.HitCount.Should().Be(2);
        preview.Resolution.Should().Be(CatalogueAuthorityResolution.SeveralProgrammes);
        preview.ImdbId.Should().BeNull();
        preview.TmdbId.Should().BeNull();
        preview.TvdbId.Should().BeNull();
        preview.HitLabels.Should().Equal(first, second);
    }

    [Fact(DisplayName =
        "A blank title is not searched, because there is no name to verify.")]
    public async Task blank_title_is_not_searched()
    {
        // Arrange
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewTvShowAsync("  ");

        // Assert
        preview.HitCount.Should().Be(0);
        preview.ImdbId.Should().BeNull();
        preview.TmdbId.Should().BeNull();
        preview.TvdbId.Should().BeNull();
    }

    private string ImdbId() =>
        "tt" + _fixture.CreateAppleId().ToString(CultureInfo.InvariantCulture);

    private int CreateTmdbId()
    {
        var value = (int)(_fixture.CreateAppleId() % 1_000_000_000L);
        return value == 0 ? 1 : value;
    }

    private static TmdbSearchHit Hit(
        int id,
        string name,
        TmdbTitleKind kind,
        int? year = null,
        string? posterPath = null,
        string? overview = null) =>
        new(id, name, kind, year, posterPath, overview);

    private static TheTvdbSearchHit SeriesHit(long id, string name) =>
        new(id, name, TheTvdbSearchType.Series, null, null, null, null, null, null, null);
}
