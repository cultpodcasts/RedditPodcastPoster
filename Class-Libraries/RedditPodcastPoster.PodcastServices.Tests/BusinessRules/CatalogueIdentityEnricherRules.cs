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
using RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Fixtures;
using RedditPodcastPoster.Tmdb.Clients;
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
        "When several film matches share a year and the create has that year and a runtime that agrees with TMDB, " +
        "the single year match is applied without a prompt.")]
    public async Task ambiguous_film_narrowed_by_year_is_unique()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var year = DomainTestFixture.UtcToday.Year - 2;
        var matchingId = CreateTmdbId();
        var otherId = CreateDistinctTmdbId(matchingId);
        var imdbId = ImdbId();
        var length = TimeSpan.FromMinutes(81);
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
            .ReturnsAsync(new CatalogueAuthorityIds(imdbId, matchingId, null));
        _mocker.GetMock<ITmdbClient>()
            .Setup(x => x.GetMovieMetadataAsync(matchingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbMovieMetadata(matchingId, title, null, null, 80, null, null, []));
        var sut = _mocker.CreateInstance<CatalogueIdentityEnricher>();

        // Act
        var admission = await sut.ResolveFilmAsync(
            title,
            year,
            CatalogueIdentityChoice.Unset,
            selectedKey: null,
            length: length);

        // Assert
        admission.Hold.Should().BeFalse();
        admission.Ids!.TmdbId.Should().Be(matchingId);
        admission.Ids.ImdbId.Should().Be(imdbId);
    }

    [Fact(DisplayName =
        "When several film matches share a year but the year match's TMDB runtime is far outside the source length " +
        "band, create offers the picker instead of auto-applying the short wrong title (RPP #1497).")]
    public async Task year_narrowed_film_with_disagreeing_runtime_holds_candidates()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var year = DomainTestFixture.UtcToday.Year - 5;
        var matchingId = CreateTmdbId();
        var otherId = CreateDistinctTmdbId(matchingId);
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
        _mocker.GetMock<ITmdbClient>()
            .Setup(x => x.GetMovieMetadataAsync(matchingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbMovieMetadata(matchingId, title, null, null, 11, null, null, []));
        var sut = _mocker.CreateInstance<CatalogueIdentityEnricher>();

        // Act
        var admission = await sut.ResolveFilmAsync(
            title,
            year,
            CatalogueIdentityChoice.Unset,
            selectedKey: null,
            length: TimeSpan.FromMinutes(81));

        // Assert
        admission.Hold.Should().BeTrue();
        admission.Ids.Should().BeNull();
        admission.Prompt!.Candidates.Should().HaveCount(2);
        _mocker.GetMock<ICatalogueAuthorityLookup>().Verify(
            x => x.CollectFilmAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "Edit-time film Look up offers a unique TMDB match as a single-candidate picker so the curator confirms " +
        "before blank identity fields are filled (RPP #1497).")]
    public async Task lookup_film_unique_match_requires_confirmation()
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
        var admission = await sut.LookupFilmAsync(
            title,
            year: null,
            CatalogueIdentityChoice.Unset,
            selectedKey: null);

        // Assert
        admission.Hold.Should().BeTrue();
        admission.Ids.Should().BeNull();
        admission.Prompt!.Candidates.Should().ContainSingle(c => c.TmdbId == tmdbId);
    }

    [Fact(DisplayName =
        "When the single year-narrowed film candidate has no positive TMDB id, create holds candidates " +
        "instead of collecting film id 0.")]
    public async Task year_narrowed_film_without_tmdb_id_holds_candidates()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var year = DomainTestFixture.UtcToday.Year - 1;
        var otherId = CreateTmdbId();
        var candidates = new[]
        {
            new CatalogueAuthorityCandidate(
                CatalogueAuthorityCandidate.TvdbKey(_fixture.CreateAppleId()),
                title,
                year,
                CatalogueAuthorityCandidate.FilmKind,
                CatalogueAuthorityCandidate.TvdbSource,
                TmdbId: null,
                TvdbId: _fixture.CreateAppleId()),
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
        var sut = _mocker.CreateInstance<CatalogueIdentityEnricher>();

        // Act
        var admission = await sut.ResolveFilmAsync(
            title,
            year,
            CatalogueIdentityChoice.Unset,
            selectedKey: null);

        // Assert
        admission.Hold.Should().BeTrue();
        admission.Ids.Should().BeNull();
        admission.Prompt!.Candidates.Should().HaveCount(2);
        _mocker.GetMock<ICatalogueAuthorityLookup>().Verify(
            x => x.CollectFilmAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
            existingTvdb + 1);
        var sut = _mocker.CreateInstance<CatalogueIdentityEnricher>();

        // Act
        var changed = sut.ApplyMissing(show, incoming);

        // Assert
        changed.Should().BeFalse();
        show.ImdbId.Should().Be(existingImdb);
        show.TmdbId.Should().Be(existingTmdb);
        show.TvdbId.Should().Be(existingTvdb);
    }

    [Fact(DisplayName =
        "When the only film match has a different title, admission holds that single candidate for the curator to accept " +
        "rather than reporting no match, and does not apply it even when the year agrees (issue #1421).")]
    public async Task single_different_title_candidate_is_offered_not_applied()
    {
        // Arrange
        var candidate = new CatalogueAuthorityCandidate(
            CatalogueAuthorityCandidate.TmdbKey(FaderhusetFilm.TmdbId),
            FaderhusetFilm.TmdbTitle,
            FaderhusetFilm.Year,
            CatalogueAuthorityCandidate.FilmKind,
            CatalogueAuthorityCandidate.TmdbSource,
            FaderhusetFilm.TmdbId,
            TvdbId: null);
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(x => x.PreviewFilmAsync(FaderhusetFilm.Query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityPreview(
                FaderhusetFilm.Query,
                HitCount: 1,
                ImdbId: null,
                TmdbId: null,
                TvdbId: null,
                [FaderhusetFilm.TmdbTitle],
                Resolution: CatalogueAuthorityResolution.DifferentTitle,
                Candidates: [candidate]));
        var sut = _mocker.CreateInstance<CatalogueIdentityEnricher>();

        // Act
        var admission = await sut.ResolveFilmAsync(
            FaderhusetFilm.Query,
            FaderhusetFilm.Year,
            CatalogueIdentityChoice.Unset,
            selectedKey: null);

        // Assert
        admission.Hold.Should().BeTrue();
        admission.Ids.Should().BeNull();
        admission.Prompt!.Target.Should().Be(CatalogueAuthorityCandidate.FilmKind);
        admission.Prompt.Query.Should().Be(FaderhusetFilm.Query);
        admission.Prompt.Candidates.Should().ContainSingle()
            .Which.Key.Should().Be(CatalogueAuthorityCandidate.TmdbKey(FaderhusetFilm.TmdbId));
        _mocker.GetMock<ICatalogueAuthorityLookup>()
            .Verify(x => x.CollectFilmAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "When the curator selects an IMDb-only film candidate, admission stores that IMDb id alone, because TMDB had no record for it.")]
    public async Task select_imdb_key_applies_imdb_id_only()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var imdbId = ImdbId();
        var sut = _mocker.CreateInstance<CatalogueIdentityEnricher>();
        var film = new Film(title);

        // Act
        var admission = await sut.ResolveFilmAsync(
            title,
            year: null,
            CatalogueIdentityChoice.Select,
            CatalogueAuthorityCandidate.ImdbKey(imdbId));
        sut.ApplyMissing(film, admission.Ids);

        // Assert
        admission.Hold.Should().BeFalse();
        admission.Ids!.ImdbId.Should().Be(imdbId);
        admission.Ids.TmdbId.Should().BeNull();
        film.ImdbId.Should().Be(imdbId);
        film.TmdbId.Should().BeNull();
        _mocker.GetMock<ICatalogueAuthorityLookup>()
            .Verify(x => x.CollectFilmAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "Edit-time TV show lookup: when TMDB TV and TheTVDB offer nothing, film/documentary hits for the same title " +
        "are held for the curator to pick and no ids are applied (#1387).")]
    public async Task lookup_tv_show_offers_film_candidates_when_tv_has_none()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var imdbId = ImdbId();
        var film = new CatalogueAuthorityCandidate(
            CatalogueAuthorityCandidate.ImdbKey(imdbId),
            name,
            Year: null,
            CatalogueAuthorityCandidate.FilmKind,
            CatalogueAuthorityCandidate.ImdbSource,
            TmdbId: null,
            TvdbId: null);
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(x => x.PreviewTvShowAsync(name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityPreview(name, 0, null, null, null, []));
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(x => x.PreviewTvShowOtherKindsAsync(name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityPreview(
                name, 1, null, null, null, [name],
                Resolution: CatalogueAuthorityResolution.DifferentTitle,
                Candidates: [film]));
        var sut = _mocker.CreateInstance<CatalogueIdentityEnricher>();

        // Act
        var admission = await sut.LookupTvShowAsync(name, CatalogueIdentityChoice.Unset, selectedKey: null);

        // Assert
        admission.Hold.Should().BeTrue();
        admission.Ids.Should().BeNull();
        admission.Prompt!.Target.Should().Be(CatalogueAuthorityCandidate.TvShowKind);
        admission.Prompt.Candidates.Should().ContainSingle().Which.Key.Should().Be(film.Key);
    }

    [Fact(DisplayName =
        "Edit-time TV show lookup: a confident TV match is used as before and film/documentary search is not asked (#1387).")]
    public async Task lookup_tv_show_unique_tv_match_skips_film_search()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var imdbId = ImdbId();
        var tmdbId = CreateTmdbId();
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(x => x.PreviewTvShowAsync(name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityPreview(
                name, 1, imdbId, tmdbId, null, [name], Resolution: CatalogueAuthorityResolution.Unique));
        var sut = _mocker.CreateInstance<CatalogueIdentityEnricher>();

        // Act
        var admission = await sut.LookupTvShowAsync(name, CatalogueIdentityChoice.Unset, selectedKey: null);

        // Assert
        admission.Hold.Should().BeFalse();
        admission.Ids!.ImdbId.Should().Be(imdbId);
        admission.Ids.TmdbId.Should().Be(tmdbId);
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Verify(x => x.PreviewTvShowOtherKindsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory(DisplayName =
        "Edit-time TV show lookup: when the TV authorities offer candidates (several programmes or a different " +
        "title), the TV prompt is used and film/documentary search is never asked (#1387).")]
    [InlineData(CatalogueAuthorityResolution.SeveralProgrammes)]
    [InlineData(CatalogueAuthorityResolution.DifferentTitle)]
    public async Task lookup_tv_show_with_tv_candidates_prompts_tv(CatalogueAuthorityResolution resolution)
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var tvdbId = _fixture.CreateAppleId();
        var tv = new CatalogueAuthorityCandidate(
            CatalogueAuthorityCandidate.TvdbKey(tvdbId),
            name,
            Year: null,
            CatalogueAuthorityCandidate.TvShowKind,
            CatalogueAuthorityCandidate.TvdbSource,
            TmdbId: null,
            tvdbId);
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(x => x.PreviewTvShowAsync(name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityPreview(
                name, 2, null, null, null, [name], Resolution: resolution, Candidates: [tv]));
        var sut = _mocker.CreateInstance<CatalogueIdentityEnricher>();

        // Act
        var admission = await sut.LookupTvShowAsync(name, CatalogueIdentityChoice.Unset, selectedKey: null);

        // Assert
        admission.Hold.Should().BeTrue();
        admission.Prompt!.Candidates.Should().ContainSingle().Which.Key.Should().Be(tv.Key);
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Verify(x => x.PreviewTvShowOtherKindsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "Edit-time TV show lookup: when the film/documentary search throws, the result is the same no-match as " +
        "before (no ids, no prompt), not an error (#1387).")]
    public async Task lookup_tv_show_film_search_failure_is_no_match()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(x => x.PreviewTvShowAsync(name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityPreview(name, 0, null, null, null, []));
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(x => x.PreviewTvShowOtherKindsAsync(name, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException(_fixture.CreateTitle()));
        var sut = _mocker.CreateInstance<CatalogueIdentityEnricher>();

        // Act
        var admission = await sut.LookupTvShowAsync(name, CatalogueIdentityChoice.Unset, selectedKey: null);

        // Assert
        admission.Hold.Should().BeFalse();
        admission.Ids.Should().BeNull();
        admission.Prompt.Should().BeNull();
    }

    [Fact(DisplayName =
        "Edit-time TV show lookup: a single TV hit under a different title with no candidates to pick falls " +
        "through to the film/documentary search (#1387).")]
    public async Task lookup_tv_show_single_disagreeing_tv_hit_asks_film_search()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var imdbId = ImdbId();
        var film = new CatalogueAuthorityCandidate(
            CatalogueAuthorityCandidate.ImdbKey(imdbId),
            name,
            Year: null,
            CatalogueAuthorityCandidate.FilmKind,
            CatalogueAuthorityCandidate.ImdbSource,
            TmdbId: null,
            TvdbId: null);
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(x => x.PreviewTvShowAsync(name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityPreview(
                name, 1, null, null, null, [_fixture.CreateTitleNotContainedIn(name)],
                Resolution: CatalogueAuthorityResolution.DifferentTitle));
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(x => x.PreviewTvShowOtherKindsAsync(name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityPreview(
                name, 1, null, null, null, [name],
                Resolution: CatalogueAuthorityResolution.DifferentTitle,
                Candidates: [film]));
        var sut = _mocker.CreateInstance<CatalogueIdentityEnricher>();

        // Act
        var admission = await sut.LookupTvShowAsync(name, CatalogueIdentityChoice.Unset, selectedKey: null);

        // Assert
        admission.Hold.Should().BeTrue();
        admission.Prompt!.Candidates.Should().ContainSingle().Which.Key.Should().Be(film.Key);
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Verify(x => x.PreviewTvShowOtherKindsAsync(name, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "Create-time TV show resolve never asks film/documentary search, so a submit or move is not held on a film " +
        "guess (#1387).")]
    public async Task resolve_tv_show_does_not_offer_film_candidates()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(x => x.PreviewTvShowAsync(name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityPreview(name, 0, null, null, null, []));
        var sut = _mocker.CreateInstance<CatalogueIdentityEnricher>();

        // Act
        var admission = await sut.ResolveTvShowAsync(name, CatalogueIdentityChoice.Unset, selectedKey: null);

        // Assert
        admission.Hold.Should().BeFalse();
        admission.Ids.Should().BeNull();
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Verify(x => x.PreviewTvShowOtherKindsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "When the curator picks an IMDb-keyed film for a TV show, only the IMDb id is stored and no TV ids are " +
        "collected (#1387).")]
    public async Task select_imdb_key_for_tv_show_stores_imdb_id_only()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var imdbId = ImdbId();
        var show = new TvShow(name);
        var sut = _mocker.CreateInstance<CatalogueIdentityEnricher>();

        // Act
        var admission = await sut.LookupTvShowAsync(
            name,
            CatalogueIdentityChoice.Select,
            CatalogueAuthorityCandidate.ImdbKey(imdbId));
        sut.ApplyMissing(show, admission.Ids);

        // Assert
        admission.Hold.Should().BeFalse();
        admission.Ids!.ImdbId.Should().Be(imdbId);
        admission.Ids.TmdbId.Should().BeNull();
        admission.Ids.TvdbId.Should().BeNull();
        show.ImdbId.Should().Be(imdbId);
        show.TmdbId.Should().BeNull();
        _mocker.GetMock<ICatalogueAuthorityLookup>()
            .Verify(x => x.CollectTvShowAsync(It.IsAny<int?>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()),
                Times.Never);
        // The edit-time select goes straight to SelectTvShowAsync: no TV or film search is re-run.
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Verify(x => x.PreviewTvShowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Verify(x => x.PreviewTvShowOtherKindsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
