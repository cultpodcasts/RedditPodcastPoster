using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Tmdb.Models;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules;

public class CatalogueIdentityEnricherRules
{
    private readonly AutoMocker _mocker = new();
    private readonly DomainTestFixture _fixture = new();

    public CatalogueIdentityEnricherRules()
    {
        _mocker.Use<ILogger<CatalogueIdentityEnricher>>(NullLogger<CatalogueIdentityEnricher>.Instance);
    }

    [Fact(DisplayName =
        "When TMDB finds one confident film match, identity admission supplies IMDb and TMDB ids so create can store them.")]
    public async Task unique_film_match_supplies_ids()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var tmdbId = CreateTmdbId();
        var imdbId = ImdbId();
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(x => x.PreviewFilmAsync(title, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityPreview(
                title,
                HitCount: 1,
                imdbId,
                tmdbId,
                TvdbId: null,
                [title],
                Resolution: CatalogueAuthorityResolution.Unique));
        var sut = _mocker.CreateInstance<CatalogueIdentityEnricher>();

        // Act
        var admission = await sut.ResolveFilmAsync(
            title,
            year: null,
            CatalogueIdentityChoice.Unset,
            selectedKey: null);

        // Assert
        admission.Hold.Should().BeFalse();
        admission.Ids.Should().NotBeNull();
        admission.Ids!.ImdbId.Should().Be(imdbId);
        admission.Ids.TmdbId.Should().Be(tmdbId);
        var film = new Film(title);
        sut.ApplyMissing(film, admission.Ids).Should().BeTrue();
        film.ImdbId.Should().Be(imdbId);
        film.TmdbId.Should().Be(tmdbId);
    }

    [Fact(DisplayName =
        "When TMDB finds several film matches, nothing is chosen and the admission holds candidate options for the curator.")]
    public async Task ambiguous_film_holds_candidates()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var firstId = CreateTmdbId();
        var secondId = CreateDistinctTmdbId(firstId);
        var first = _fixture.CreateTitle();
        var second = _fixture.CreateTitle();
        var candidates = new[]
        {
            new CatalogueAuthorityCandidate(
                CatalogueAuthorityCandidate.TmdbKey(firstId),
                first,
                Year: null,
                CatalogueAuthorityCandidate.FilmKind,
                CatalogueAuthorityCandidate.TmdbSource,
                firstId,
                TvdbId: null),
            new CatalogueAuthorityCandidate(
                CatalogueAuthorityCandidate.TmdbKey(secondId),
                second,
                Year: null,
                CatalogueAuthorityCandidate.FilmKind,
                CatalogueAuthorityCandidate.TmdbSource,
                secondId,
                TvdbId: null)
        };
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(x => x.PreviewFilmAsync(title, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityPreview(
                title,
                HitCount: 2,
                ImdbId: null,
                TmdbId: null,
                TvdbId: null,
                [first, second],
                Resolution: CatalogueAuthorityResolution.SeveralProgrammes,
                Candidates: candidates));
        var sut = _mocker.CreateInstance<CatalogueIdentityEnricher>();

        // Act
        var admission = await sut.ResolveFilmAsync(
            title,
            year: null,
            CatalogueIdentityChoice.Unset,
            selectedKey: null);

        // Assert
        admission.Hold.Should().BeTrue();
        admission.Ids.Should().BeNull();
        admission.Prompt!.Candidates.Should().HaveCount(2);
        admission.Prompt.Candidates[0].Key.Should().Be(CatalogueAuthorityCandidate.TmdbKey(firstId));
        admission.Prompt.Candidates[1].Key.Should().Be(CatalogueAuthorityCandidate.TmdbKey(secondId));
    }

    [Fact(DisplayName =
        "When several film matches share a year and the create has that year, the single year match is applied without a prompt.")]
    public async Task ambiguous_film_narrowed_by_year_is_unique()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var year = DomainTestFixture.UtcToday.Year - 2;
        var matchingId = CreateTmdbId();
        var otherId = CreateDistinctTmdbId(matchingId);
        var imdbId = ImdbId();
        var candidates = new[]
        {
            new CatalogueAuthorityCandidate(
                CatalogueAuthorityCandidate.TmdbKey(matchingId),
                title,
                year,
                CatalogueAuthorityCandidate.FilmKind,
                CatalogueAuthorityCandidate.TmdbSource,
                matchingId,
                TvdbId: null),
            new CatalogueAuthorityCandidate(
                CatalogueAuthorityCandidate.TmdbKey(otherId),
                title,
                year - 1,
                CatalogueAuthorityCandidate.FilmKind,
                CatalogueAuthorityCandidate.TmdbSource,
                otherId,
                TvdbId: null)
        };
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(x => x.PreviewFilmAsync(title, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityPreview(
                title,
                HitCount: 2,
                ImdbId: null,
                TmdbId: null,
                TvdbId: null,
                [title, title],
                Resolution: CatalogueAuthorityResolution.SeveralProgrammes,
                Candidates: candidates));
        _mocker.GetMock<ICatalogueAuthorityLookup>()
            .Setup(x => x.CollectFilmAsync(matchingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityIds(imdbId, matchingId, null, null, null));
        var sut = _mocker.CreateInstance<CatalogueIdentityEnricher>();

        // Act
        var admission = await sut.ResolveFilmAsync(
            title,
            year,
            CatalogueIdentityChoice.Unset,
            selectedKey: null);

        // Assert
        admission.Hold.Should().BeFalse();
        admission.Ids!.TmdbId.Should().Be(matchingId);
        admission.Ids.ImdbId.Should().Be(imdbId);
    }

    [Fact(DisplayName =
        "When no authority hit is found, create continues with null identity rather than inventing ids.")]
    public async Task no_match_leaves_identity_null()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(x => x.PreviewFilmAsync(title, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityPreview(
                title,
                HitCount: 0,
                ImdbId: null,
                TmdbId: null,
                TvdbId: null,
                [],
                Resolution: CatalogueAuthorityResolution.NotInAuthority));
        var sut = _mocker.CreateInstance<CatalogueIdentityEnricher>();

        // Act
        var admission = await sut.ResolveFilmAsync(
            title,
            year: null,
            CatalogueIdentityChoice.Unset,
            selectedKey: null);

        // Assert
        admission.Hold.Should().BeFalse();
        admission.Ids.Should().BeNull();
        var film = new Film(title);
        sut.ApplyMissing(film, admission.Ids).Should().BeFalse();
        film.ImdbId.Should().BeNull();
        film.TmdbId.Should().BeNull();
    }

    [Fact(DisplayName =
        "When the curator continues without identity, admission proceeds with no ids and create may finish.")]
    public async Task continue_skips_identity()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var sut = _mocker.CreateInstance<CatalogueIdentityEnricher>();

        // Act
        var admission = await sut.ResolveFilmAsync(
            title,
            year: null,
            CatalogueIdentityChoice.Continue,
            selectedKey: null);

        // Assert
        admission.Hold.Should().BeFalse();
        admission.Ids.Should().BeNull();
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Verify(x => x.PreviewFilmAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "When the curator selects a TMDB candidate key, that film's collected ids are applied and blanks on an existing film are filled only.")]
    public async Task select_applies_chosen_ids_without_overwriting_existing()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var tmdbId = CreateTmdbId();
        var imdbId = ImdbId();
        var existingImdb = ImdbId();
        _mocker.GetMock<ICatalogueAuthorityLookup>()
            .Setup(x => x.CollectFilmAsync(tmdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityIds(
                imdbId,
                tmdbId,
                null,
                CatalogueCanonicalId.ImdbPage(imdbId),
                null));
        var sut = _mocker.CreateInstance<CatalogueIdentityEnricher>();
        var film = new Film(title) { ImdbId = existingImdb };

        // Act
        var admission = await sut.ResolveFilmAsync(
            title,
            year: null,
            CatalogueIdentityChoice.Select,
            CatalogueAuthorityCandidate.TmdbKey(tmdbId));
        sut.ApplyMissing(film, admission.Ids);

        // Assert
        admission.Hold.Should().BeFalse();
        admission.Ids!.TmdbId.Should().Be(tmdbId);
        film.ImdbId.Should().Be(existingImdb);
        film.TmdbId.Should().Be(tmdbId);
    }

    [Fact(DisplayName =
        "When a TV show already has identity ids, fill-missing leaves those ids alone even when lookup would return others.")]
    public void existing_tv_show_ids_are_preserved()
    {
        // Arrange
        var existingImdb = ImdbId();
        var existingTmdb = CreateTmdbId();
        var existingTvdb = _fixture.CreateAppleId();
        var show = new TvShow(_fixture.CreateTitle())
        {
            ImdbId = existingImdb,
            TmdbId = existingTmdb,
            TvdbId = existingTvdb
        };
        var incoming = new CatalogueAuthorityIds(
            ImdbId(),
            CreateDistinctTmdbId(existingTmdb),
            existingTvdb + 1,
            null,
            null);
        var sut = _mocker.CreateInstance<CatalogueIdentityEnricher>();

        // Act
        var changed = sut.ApplyMissing(show, incoming);

        // Assert
        changed.Should().BeFalse();
        show.ImdbId.Should().Be(existingImdb);
        show.TmdbId.Should().Be(existingTmdb);
        show.TvdbId.Should().Be(existingTvdb);
    }

    private string ImdbId() =>
        "tt" + _fixture.CreateAppleId().ToString(CultureInfo.InvariantCulture);

    private int CreateTmdbId()
    {
        var value = (int)(_fixture.CreateAppleId() % 1_000_000_000L);
        return value == 0 ? 1 : value;
    }

    private int CreateDistinctTmdbId(int other)
    {
        var candidate = CreateTmdbId();
        return candidate == other ? candidate + 1 : candidate;
    }
}
