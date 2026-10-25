using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Dtos;
using Api.Services.Catalogue;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Catalogue;

public class CatalogueIdentityLookupRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private CatalogueIdentityAdmission _admission = CatalogueIdentityAdmission.Proceed();

    public CatalogueIdentityLookupRules()
    {
        _mocker.Use(NullLogger<CatalogueIdentityLookupService>.Instance);
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(x => x.ResolveFilmAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<CatalogueIdentityChoice>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _admission);
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(x => x.ResolveTvShowAsync(
                It.IsAny<string>(),
                It.IsAny<CatalogueIdentityChoice>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _admission);
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(x => x.ResolveTvShowEpisodeAsync(
                It.IsAny<RedditPodcastPoster.Models.TvShows.TvShow>(),
                It.IsAny<string>(),
                It.IsAny<CatalogueIdentityChoice>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _admission);
    }

    [Fact(DisplayName =
        "Edit-time identity lookup: when authority finds one confident film match, the response is matched with ids so the form can be filled.")]
    public async Task confident_film_returns_matched_ids()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var imdbId = _fixture.CreateImdbId();
        var tmdbId = _fixture.CreateTmdbId();
        _admission = CatalogueIdentityAdmission.Proceed(
            new CatalogueAuthorityIds(imdbId, tmdbId, null));
        var sut = _mocker.CreateInstance<CatalogueIdentityLookupService>();

        // Act
        var result = await sut.LookupAsync(new CatalogueIdentityLookupRequest
        {
            Kind = "Film",
            Title = title
        });

        // Assert
        result.Status.Should().Be(CatalogueIdentityLookupServiceStatus.Ok);
        result.Response!.Status.Should().Be(CatalogueIdentityLookupStatus.Matched);
        result.Response.Ids!.ImdbId.Should().Be(imdbId);
        result.Response.Ids.TmdbId.Should().Be(tmdbId);
        result.Response.IdentityDecision.Should().BeNull();
    }

    [Fact(DisplayName =
        "Edit-time identity lookup: when authority finds several film matches, the response is ambiguous with candidates and does not auto-pick one.")]
    public async Task ambiguous_film_returns_candidates_without_auto_pick()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var firstTitle = _fixture.CreateTitle();
        var secondTitle = _fixture.CreateTitle();
        var firstId = _fixture.CreateTmdbId();
        var secondId = firstId + 1;
        var candidates = new[]
        {
            new CatalogueAuthorityCandidate(
                CatalogueAuthorityCandidate.TmdbKey(firstId),
                firstTitle,
                Year: null,
                CatalogueAuthorityCandidate.FilmKind,
                CatalogueAuthorityCandidate.TmdbSource,
                firstId,
                TvdbId: null),
            new CatalogueAuthorityCandidate(
                CatalogueAuthorityCandidate.TmdbKey(secondId),
                secondTitle,
                Year: null,
                CatalogueAuthorityCandidate.FilmKind,
                CatalogueAuthorityCandidate.TmdbSource,
                secondId,
                TvdbId: null)
        };
        _admission = CatalogueIdentityAdmission.NeedsPrompt(
            new CatalogueIdentityHold(CatalogueAuthorityCandidate.FilmKind, title, candidates));
        var sut = _mocker.CreateInstance<CatalogueIdentityLookupService>();

        // Act
        var result = await sut.LookupAsync(new CatalogueIdentityLookupRequest
        {
            Kind = "Film",
            Title = title
        });

        // Assert
        result.Status.Should().Be(CatalogueIdentityLookupServiceStatus.Ok);
        result.Response!.Status.Should().Be(CatalogueIdentityLookupStatus.Ambiguous);
        result.Response.Ids.Should().BeNull();
        result.Response.IdentityDecision.Should().NotBeNull();
        result.Response.IdentityDecision!.Candidates.Should().HaveCount(2);
        result.Response.IdentityDecision.Candidates.Select(c => c.Key)
            .Should().BeEquivalentTo(candidates.Select(c => c.Key));
    }

    [Fact(DisplayName =
        "Edit-time identity lookup: when the curator selects one ambiguous candidate, the response is matched with that candidate's ids.")]
    public async Task select_applies_chosen_candidate_ids()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var imdbId = _fixture.CreateImdbId();
        var tmdbId = _fixture.CreateTmdbId();
        var selectedKey = CatalogueAuthorityCandidate.TmdbKey(tmdbId);
        _admission = CatalogueIdentityAdmission.Proceed(
            new CatalogueAuthorityIds(imdbId, tmdbId, null));
        var sut = _mocker.CreateInstance<CatalogueIdentityLookupService>();

        // Act
        var result = await sut.LookupAsync(new CatalogueIdentityLookupRequest
        {
            Kind = "Film",
            Title = title,
            IdentityDecision = "select",
            SelectedIdentityKey = selectedKey
        });

        // Assert
        result.Status.Should().Be(CatalogueIdentityLookupServiceStatus.Ok);
        result.Response!.Status.Should().Be(CatalogueIdentityLookupStatus.Matched);
        result.Response.Ids!.TmdbId.Should().Be(tmdbId);
        _mocker.GetMock<ICatalogueIdentityEnricher>().Verify(
            x => x.ResolveFilmAsync(
                title,
                null,
                CatalogueIdentityChoice.Select,
                selectedKey,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "Edit-time identity lookup: when the request includes a film year, resolve is called with that year " +
        "so a single year match can auto-narrow (same as create-time).")]
    public async Task film_year_is_forwarded_to_resolve()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var year = DomainTestFixture.UtcToday.Year - 3;
        var imdbId = _fixture.CreateImdbId();
        var tmdbId = _fixture.CreateTmdbId();
        _admission = CatalogueIdentityAdmission.Proceed(new CatalogueAuthorityIds(imdbId, tmdbId, null));
        var sut = _mocker.CreateInstance<CatalogueIdentityLookupService>();

        // Act
        var result = await sut.LookupAsync(new CatalogueIdentityLookupRequest
        {
            Kind = "Film",
            Title = title,
            Year = year
        });

        // Assert
        result.Status.Should().Be(CatalogueIdentityLookupServiceStatus.Ok);
        result.Response!.Status.Should().Be(CatalogueIdentityLookupStatus.Matched);
        _mocker.GetMock<ICatalogueIdentityEnricher>().Verify(
            x => x.ResolveFilmAsync(
                title,
                year,
                CatalogueIdentityChoice.Unset,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "Edit-time identity lookup: when candidates carry page, thumbnail, and description, " +
        "the ambiguous response maps those fields for the curator picker.")]
    public async Task ambiguous_maps_page_thumbnail_and_description()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var candidateTitle = _fixture.CreateTitle();
        var tmdbId = _fixture.CreateTmdbId();
        var page = new Uri($"https://www.themoviedb.org/movie/{tmdbId}");
        var thumb = new Uri($"https://image.tmdb.org/t/p/w185/poster{tmdbId}.jpg");
        var description = _fixture.CreateTitle();
        var candidates = new[]
        {
            new CatalogueAuthorityCandidate(
                CatalogueAuthorityCandidate.TmdbKey(tmdbId),
                candidateTitle,
                Year: null,
                CatalogueAuthorityCandidate.FilmKind,
                CatalogueAuthorityCandidate.TmdbSource,
                tmdbId,
                TvdbId: null,
                page,
                thumb,
                description)
        };
        _admission = CatalogueIdentityAdmission.NeedsPrompt(
            new CatalogueIdentityHold(CatalogueAuthorityCandidate.FilmKind, title, candidates));
        var sut = _mocker.CreateInstance<CatalogueIdentityLookupService>();

        // Act
        var result = await sut.LookupAsync(new CatalogueIdentityLookupRequest
        {
            Kind = "Film",
            Title = title
        });

        // Assert
        var body = result.Response!.IdentityDecision!.Candidates.Single();
        body.PageUrl.Should().Be(page.ToString());
        body.ThumbnailUrl.Should().Be(thumb.ToString());
        body.Description.Should().Be(description);
    }

    [Fact(DisplayName =
        "Edit-time identity lookup: when authority finds no confident match, the response is noMatch with a message and no ids so form fields stay alone.")]
    public async Task no_match_leaves_fields_alone()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        _admission = CatalogueIdentityAdmission.Proceed();
        var sut = _mocker.CreateInstance<CatalogueIdentityLookupService>();

        // Act
        var result = await sut.LookupAsync(new CatalogueIdentityLookupRequest
        {
            Kind = "TvShow",
            Title = title
        });

        // Assert
        result.Status.Should().Be(CatalogueIdentityLookupServiceStatus.Ok);
        result.Response!.Status.Should().Be(CatalogueIdentityLookupStatus.NoMatch);
        result.Response.Ids.Should().BeNull();
        result.Response.Message.Should().NotBeNullOrWhiteSpace();
        result.Response.IdentityDecision.Should().BeNull();
    }

    [Fact(DisplayName =
        "Edit-time identity lookup: blank kind or title is a bad request.")]
    public async Task blank_title_is_bad_request()
    {
        // Arrange
        var sut = _mocker.CreateInstance<CatalogueIdentityLookupService>();

        // Act
        var result = await sut.LookupAsync(new CatalogueIdentityLookupRequest
        {
            Kind = "Film",
            Title = "   "
        });

        // Assert
        result.Status.Should().Be(CatalogueIdentityLookupServiceStatus.BadRequest);
        result.Response.Should().BeNull();
    }

    [Fact(DisplayName =
        "Edit-time identity lookup: when an episode resolves with parent ids from a show that already has identity, " +
        "the matched response carries both episode ids and parentIds.")]
    public async Task episode_matched_includes_parent_ids()
    {
        // Arrange
        var episodeTitle = _fixture.CreateTitle();
        var showName = _fixture.CreateTitle();
        var episodeImdb = _fixture.CreateImdbId();
        var parentImdb = _fixture.CreateImdbId();
        var showTmdb = _fixture.CreateTmdbId();
        var showTvdb = _fixture.CreateTvdbId();
        _admission = CatalogueIdentityAdmission.Proceed(
            new CatalogueAuthorityIds(episodeImdb, null, null),
            new CatalogueAuthorityIds(parentImdb, showTmdb, showTvdb));
        var sut = _mocker.CreateInstance<CatalogueIdentityLookupService>();

        // Act
        var result = await sut.LookupAsync(new CatalogueIdentityLookupRequest
        {
            Kind = "TvShowEpisode",
            Title = episodeTitle,
            ShowName = showName,
            ShowTmdbId = showTmdb,
            ShowTvdbId = showTvdb,
            ShowImdbId = parentImdb
        });

        // Assert
        result.Response!.Status.Should().Be(CatalogueIdentityLookupStatus.Matched);
        result.Response.Ids!.ImdbId.Should().Be(episodeImdb);
        result.Response.ParentIds!.ImdbId.Should().Be(parentImdb);
        result.Response.ParentIds.TmdbId.Should().Be(showTmdb);
        result.Response.ParentIds.TvdbId.Should().Be(showTvdb);
        _mocker.GetMock<ICatalogueIdentityEnricher>().Verify(
            x => x.ResolveTvShowEpisodeAsync(
                It.Is<RedditPodcastPoster.Models.TvShows.TvShow>(s =>
                    s.Name == showName &&
                    s.TmdbId == showTmdb &&
                    s.TvdbId == showTvdb &&
                    s.ImdbId == parentImdb),
                episodeTitle,
                CatalogueIdentityChoice.Unset,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
