using System.Globalization;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Catalogue.Authority.Imdb;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Fixtures;
using RedditPodcastPoster.TheTvdb.Clients;
using RedditPodcastPoster.TheTvdb.Models;
using RedditPodcastPoster.Tmdb.Clients;
using RedditPodcastPoster.Tmdb.Models;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules;

public class CatalogueAuthorityPreviewRules
{
    private readonly AutoMocker _mocker = new();
    private readonly DomainTestFixture _fixture = new();
    private IReadOnlyList<ImdbSuggestion> _imdbSuggestions = [];

    public CatalogueAuthorityPreviewRules()
    {
        _mocker.GetMock<IImdbSuggestionClient>()
            .Setup(client => client.SuggestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _imdbSuggestions);
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
        "Several TV TMDB hits expose a /tv/ page URL, poster thumbnail, and overview description on each ambiguous candidate.")]
    public async Task many_tv_tmdb_hits_include_page_thumbnail_and_description()
    {
        // Arrange
        var query = _fixture.CreateTitle();
        var first = _fixture.CreateTitle();
        var second = _fixture.CreateTitle();
        var firstId = CreateTmdbId();
        var secondId = CreateTmdbId();
        var overview = _fixture.CreateTitle();
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchTvAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Hit(firstId, first, TmdbTitleKind.TvSeries, posterPath: $"/p{firstId}.jpg", overview: overview),
                Hit(secondId, second, TmdbTitleKind.TvSeries, posterPath: $"/p{secondId}.jpg", overview: overview)
            ]);
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewTvShowAsync(query);

        // Assert
        preview.Candidates.Should().NotBeNull();
        preview.Candidates!.Should().HaveCount(2);
        preview.Candidates.Should().OnlyContain(c =>
            c.Source == CatalogueAuthorityCandidate.TmdbSource &&
            c.PageUrl != null &&
            c.PageUrl.Host == "www.themoviedb.org" &&
            c.PageUrl.AbsolutePath.StartsWith("/tv/", StringComparison.Ordinal) &&
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
            .ReturnsAsync([SeriesHit(_fixture.CreateTvdbId(), first), SeriesHit(_fixture.CreateTvdbId(), second)]);
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
        "When a TVDB ambiguous series hit carries an IMDb title id, the candidate PageUrl is the IMDb page " +
        "while source remains tvdb, because IMDb is the preferred curator inspection link.")]
    public async Task ambiguous_tvdb_hit_with_imdb_prefers_imdb_page_url()
    {
        // Arrange
        var query = _fixture.CreateTitle();
        var first = _fixture.CreateTitle();
        var second = _fixture.CreateTitle();
        var firstTvdb = _fixture.CreateTvdbId();
        var secondTvdb = _fixture.CreateTvdbId();
        var imdbId = ImdbId();
        var imdbUrl = CatalogueCanonicalId.ImdbPage(imdbId);
        var tvdbCanonical = CatalogueCanonicalId.TvdbPage(firstTvdb, TmdbTitleKind.TvSeries);
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchTvAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mocker.GetMock<ITheTvdbClient>()
            .Setup(client => client.SearchAsync(query, TheTvdbSearchType.Series, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                SeriesHit(firstTvdb, first, imdbId, tvdbCanonical, imdbUrl),
                SeriesHit(secondTvdb, second)
            ]);
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewTvShowAsync(query);

        // Assert
        preview.Candidates.Should().NotBeNull();
        preview.Candidates!.Should().HaveCount(2);
        preview.Candidates.Should().OnlyContain(c => c.Source == CatalogueAuthorityCandidate.TvdbSource);
        var withImdb = preview.Candidates.Single(c => c.TvdbId == firstTvdb);
        withImdb.PageUrl.Should().Be(imdbUrl);
        withImdb.PageUrl!.Host.Should().Be("www.imdb.com");
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

    [Fact(DisplayName =
        "A film searched by its original-language title with one TMDB hit listed under an English title is that film, " +
        "because the hit's original title agrees (issue #1421: \"Faderhuset\" is TMDB's \"Father's House\").")]
    public async Task faderhuset_original_title_agrees_with_tmdb_hit()
    {
        // Arrange
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchMoviesAsync(FaderhusetFilm.Query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([FaderhusetTmdbHit(originalName: FaderhusetFilm.Query)]);
        _mocker.GetMock<ICatalogueAuthorityLookup>()
            .Setup(lookup => lookup.CollectFilmAsync(FaderhusetFilm.TmdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityIds(FaderhusetFilm.ImdbId, FaderhusetFilm.TmdbId, null));
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewFilmAsync(FaderhusetFilm.Query);

        // Assert
        preview.Resolution.Should().Be(CatalogueAuthorityResolution.Unique);
        preview.ImdbId.Should().Be(FaderhusetFilm.ImdbId);
        preview.TmdbId.Should().Be(FaderhusetFilm.TmdbId);
        preview.TvdbId.Should().BeNull();
        _mocker.GetMock<IImdbSuggestionClient>()
            .Verify(client => client.SuggestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "A film with one TMDB hit under a title that does not agree and no original title is offered as a low-confidence " +
        "candidate rather than dropped, and an IMDb suggestion for the same film is not offered twice (issue #1421).")]
    public async Task single_disagreeing_tmdb_hit_is_offered_as_candidate()
    {
        // Arrange
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchMoviesAsync(FaderhusetFilm.Query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([FaderhusetTmdbHit(originalName: null)]);
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.FindMovieByImdbIdAsync(FaderhusetFilm.ImdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FaderhusetTmdbHit(originalName: null));
        _imdbSuggestions = [FaderhusetImdbSuggestion()];
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewFilmAsync(FaderhusetFilm.Query);

        // Assert
        preview.Resolution.Should().Be(CatalogueAuthorityResolution.DifferentTitle);
        preview.ImdbId.Should().BeNull();
        preview.TmdbId.Should().BeNull();
        preview.Candidates.Should().NotBeNull();
        var candidate = preview.Candidates!.Should().ContainSingle().Subject;
        candidate.Key.Should().Be(CatalogueAuthorityCandidate.TmdbKey(FaderhusetFilm.TmdbId));
        candidate.TmdbId.Should().Be(FaderhusetFilm.TmdbId);
        candidate.Title.Should().Be(FaderhusetFilm.TmdbTitle);
        candidate.Year.Should().Be(FaderhusetFilm.Year);
        candidate.Kind.Should().Be(CatalogueAuthorityCandidate.FilmKind);
        _mocker.GetMock<ICatalogueAuthorityLookup>()
            .Verify(lookup => lookup.CollectFilmAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "A film with no TMDB hit is looked up on IMDb, which matches alternative titles; IMDb's top film is resolved to " +
        "its TMDB record and, when that record's original title agrees, both ids are reported (issue #1421).")]
    public async Task no_tmdb_hit_finds_film_through_imdb_and_tmdb_find()
    {
        // Arrange
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchMoviesAsync(FaderhusetFilm.Query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.FindMovieByImdbIdAsync(FaderhusetFilm.ImdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FaderhusetTmdbHit(originalName: FaderhusetFilm.Query));
        _mocker.GetMock<ICatalogueAuthorityLookup>()
            .Setup(lookup => lookup.CollectFilmAsync(FaderhusetFilm.TmdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityIds(FaderhusetFilm.ImdbId, FaderhusetFilm.TmdbId, null));
        _imdbSuggestions = FaderhusetImdbSuggestions();
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewFilmAsync(FaderhusetFilm.Query);

        // Assert
        preview.Resolution.Should().Be(CatalogueAuthorityResolution.Unique);
        preview.ImdbId.Should().Be(FaderhusetFilm.ImdbId);
        preview.TmdbId.Should().Be(FaderhusetFilm.TmdbId);
        preview.TvdbId.Should().BeNull();
    }

    [Fact(DisplayName =
        "A film with no TMDB hit whose IMDb top film has a different title is offered as a low-confidence IMDb candidate " +
        "when TMDB has no record for it, and fuzzy IMDb rows that neither agree nor top the list are not offered.")]
    public async Task no_tmdb_hit_offers_imdb_only_candidate()
    {
        // Arrange
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchMoviesAsync(FaderhusetFilm.Query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _imdbSuggestions = FaderhusetImdbSuggestions();
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewFilmAsync(FaderhusetFilm.Query);

        // Assert
        preview.Resolution.Should().Be(CatalogueAuthorityResolution.DifferentTitle);
        preview.ImdbId.Should().BeNull();
        var candidate = preview.Candidates.Should().ContainSingle().Subject;
        candidate.Key.Should().Be(CatalogueAuthorityCandidate.ImdbKey(FaderhusetFilm.ImdbId));
        candidate.Source.Should().Be(CatalogueAuthorityCandidate.ImdbSource);
        candidate.Title.Should().Be(FaderhusetFilm.ImdbTitle);
        candidate.Year.Should().Be(FaderhusetFilm.Year);
        candidate.TmdbId.Should().BeNull();
        candidate.PageUrl.Should().Be(new Uri($"https://www.imdb.com/title/{FaderhusetFilm.ImdbId}/"));
        candidate.Description.Should().Be(FaderhusetFilm.Cast);
    }

    [Fact(DisplayName =
        "A film with no TMDB hit and one IMDb film whose title agrees is that film even when TMDB has no record for it, " +
        "and only the IMDb id is reported.")]
    public async Task no_tmdb_hit_and_one_agreeing_imdb_film_reports_imdb_id()
    {
        // Arrange
        var query = _fixture.CreateTitle();
        var imdbId = ImdbId();
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchMoviesAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _imdbSuggestions = [new ImdbSuggestion(imdbId, query, ImdbSuggestion.MovieType, null, null, null)];
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewFilmAsync(query);

        // Assert
        preview.Resolution.Should().Be(CatalogueAuthorityResolution.Unique);
        preview.ImdbId.Should().Be(imdbId);
        preview.TmdbId.Should().BeNull();
    }

    [Fact(DisplayName =
        "A film with no TMDB hit and no IMDb film reports zero hits and no ids, because neither authority matched.")]
    public async Task no_film_hit_anywhere_reports_no_ids()
    {
        // Arrange
        var query = _fixture.CreateTitle();
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchMoviesAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _imdbSuggestions =
        [
            new ImdbSuggestion(ImdbId(), _fixture.CreateTitleNotContainedIn(query), "tvSeries", null, null, null)
        ];
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewFilmAsync(query);

        // Assert
        preview.Resolution.Should().Be(CatalogueAuthorityResolution.NotInAuthority);
        preview.HitCount.Should().Be(0);
        preview.ImdbId.Should().BeNull();
        preview.Candidates.Should().BeNull();
    }

    [Fact(DisplayName =
        "A TV show searched by its original-language title with one TMDB hit listed under another title is that show, " +
        "because the hit's original name agrees.")]
    public async Task tv_original_name_agrees_with_tmdb_hit()
    {
        // Arrange
        var query = _fixture.CreateTitle();
        var listed = _fixture.CreateTitleNotContainedIn(query);
        var tmdbId = CreateTmdbId();
        var tvdbId = _fixture.CreateAppleId();
        var imdbId = ImdbId();
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchTvAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TmdbSearchHit(tmdbId, listed, TmdbTitleKind.TvSeries, null, OriginalName: query)]);
        _mocker.GetMock<ICatalogueAuthorityLookup>()
            .Setup(lookup => lookup.CollectTvShowAsync(tmdbId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityIds(imdbId, tmdbId, tvdbId));
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewTvShowAsync(query);

        // Assert
        preview.Resolution.Should().Be(CatalogueAuthorityResolution.Unique);
        preview.TmdbId.Should().Be(tmdbId);
        preview.TvdbId.Should().Be(tvdbId);
    }

    [Fact(DisplayName =
        "A long film title with no TMDB hit whose IMDb top film is a shorter title contained in it is held for the curator, " +
        "not applied, because containment is not identity (\"The Last Stand of Waco\" must not become \"Waco\").")]
    public async Task imdb_film_contained_in_a_longer_query_is_held_not_applied()
    {
        // Arrange
        const string query = "The Last Stand of Waco";
        const string shorterFilm = "Waco";
        var imdbId = ImdbId();
        var tmdbId = CreateTmdbId();
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchMoviesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.FindMovieByImdbIdAsync(imdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbSearchHit(tmdbId, shorterFilm, TmdbTitleKind.Movie, null));
        _imdbSuggestions = [new ImdbSuggestion(imdbId, shorterFilm, ImdbSuggestion.MovieType, null, null, null)];
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewFilmAsync(query);

        // Assert
        preview.Resolution.Should().Be(CatalogueAuthorityResolution.DifferentTitle);
        preview.ImdbId.Should().BeNull();
        preview.TmdbId.Should().BeNull();
        preview.Candidates.Should().ContainSingle()
            .Which.Key.Should().Be(CatalogueAuthorityCandidate.TmdbKey(tmdbId));
        _mocker.GetMock<ICatalogueAuthorityLookup>()
            .Verify(lookup => lookup.CollectFilmAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "When a TMDB find by IMDb id fails (for example throttled), the preview still holds the TMDB candidate already " +
        "found and keeps the IMDb film as an IMDb-only candidate, rather than losing them all.")]
    public async Task failed_tmdb_find_keeps_candidates_as_a_hold()
    {
        // Arrange
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchMoviesAsync(FaderhusetFilm.Query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([FaderhusetTmdbHit(originalName: null)]);
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.FindMovieByImdbIdAsync(FaderhusetFilm.ImdbId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("TMDB request failed with status 429."));
        _imdbSuggestions = [FaderhusetImdbSuggestion()];
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewFilmAsync(FaderhusetFilm.Query);

        // Assert
        preview.Resolution.Should().Be(CatalogueAuthorityResolution.DifferentTitle);
        preview.LookupFailed.Should().BeFalse();
        preview.Candidates!.Select(c => c.Key).Should().Equal(
            CatalogueAuthorityCandidate.TmdbKey(FaderhusetFilm.TmdbId),
            CatalogueAuthorityCandidate.ImdbKey(FaderhusetFilm.ImdbId));
    }

    [Fact(DisplayName =
        "When TMDB has no hit and the TMDB find for an exactly matching IMDb film fails, the film is held as a candidate, " +
        "not applied, because the TMDB record could not be checked.")]
    public async Task failed_tmdb_find_does_not_apply_an_exact_imdb_film()
    {
        // Arrange
        var query = _fixture.CreateTitle();
        var imdbId = ImdbId();
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchMoviesAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.FindMovieByImdbIdAsync(imdbId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(_fixture.CreateTitle()));
        _imdbSuggestions = [new ImdbSuggestion(imdbId, query, ImdbSuggestion.MovieType, null, null, null)];
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewFilmAsync(query);

        // Assert
        preview.Resolution.Should().Be(CatalogueAuthorityResolution.DifferentTitle);
        preview.ImdbId.Should().BeNull();
        preview.Candidates.Should().ContainSingle()
            .Which.Key.Should().Be(CatalogueAuthorityCandidate.ImdbKey(imdbId));
    }

    [Fact(DisplayName =
        "A TV show whose only agreement is a short original name contained in a longer query is not that show, " +
        "because an original-name match must be the whole title.")]
    public async Task tv_original_name_contained_in_query_is_not_unique()
    {
        // Arrange
        var original = _fixture.CreateTitle();
        var query = $"{original} {_fixture.CreateTitleNotContainedIn(original)}";
        var listed = _fixture.CreateTitleNotContainedIn(query);
        var tmdbId = CreateTmdbId();
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchTvAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TmdbSearchHit(tmdbId, listed, TmdbTitleKind.TvSeries, null, OriginalName: original)]);
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewTvShowAsync(query);

        // Assert
        preview.Resolution.Should().NotBe(CatalogueAuthorityResolution.Unique);
        preview.TmdbId.Should().BeNull();
        _mocker.GetMock<ICatalogueAuthorityLookup>()
            .Verify(lookup => lookup.CollectTvShowAsync(
                It.IsAny<int?>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "A film whose only agreement is a short original title contained in a longer query is offered, not applied, " +
        "because an original-title match must be the whole title.")]
    public async Task film_original_title_contained_in_query_is_offered_not_applied()
    {
        // Arrange
        var original = _fixture.CreateTitle();
        var query = $"{original} {_fixture.CreateTitleNotContainedIn(original)}";
        var listed = _fixture.CreateTitleNotContainedIn(query);
        var tmdbId = CreateTmdbId();
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.SearchMoviesAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TmdbSearchHit(tmdbId, listed, TmdbTitleKind.Movie, null, OriginalName: original)]);
        var sut = _mocker.CreateInstance<CatalogueAuthorityPreviewer>();

        // Act
        var preview = await sut.PreviewFilmAsync(query);

        // Assert
        preview.Resolution.Should().Be(CatalogueAuthorityResolution.DifferentTitle);
        preview.TmdbId.Should().BeNull();
        preview.Candidates.Should().ContainSingle()
            .Which.Key.Should().Be(CatalogueAuthorityCandidate.TmdbKey(tmdbId));
    }

    private static TmdbSearchHit FaderhusetTmdbHit(string? originalName) =>
        new(
            FaderhusetFilm.TmdbId,
            FaderhusetFilm.TmdbTitle,
            TmdbTitleKind.Movie,
            FaderhusetFilm.Year,
            OriginalName: originalName);

    private static ImdbSuggestion FaderhusetImdbSuggestion() =>
        new(
            FaderhusetFilm.ImdbId,
            FaderhusetFilm.ImdbTitle,
            ImdbSuggestion.MovieType,
            FaderhusetFilm.Year,
            null,
            FaderhusetFilm.Cast);

    /// <summary>IMDb's fuzzy suggestion list: the film first, then a short, a series, and an unrelated film.</summary>
    private IReadOnlyList<ImdbSuggestion> FaderhusetImdbSuggestions() =>
    [
        FaderhusetImdbSuggestion(),
        new(ImdbId(), _fixture.CreateTitle(), "short", null, null, null),
        new(ImdbId(), _fixture.CreateTitle(), "tvSeries", null, null, null),
        new(ImdbId(), _fixture.CreateTitleNotContainedIn(FaderhusetFilm.Query), ImdbSuggestion.MovieType, null, null, null)
    ];

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

    private static TheTvdbSearchHit SeriesHit(
        long id,
        string name,
        string? imdbTitleId = null,
        Uri? canonicalUrl = null,
        Uri? imdbUrl = null) =>
        new(id, name, TheTvdbSearchType.Series, null, null, null, null, imdbTitleId, canonicalUrl, imdbUrl);
}
