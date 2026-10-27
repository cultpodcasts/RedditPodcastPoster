using System.Globalization;
using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Tmdb.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Processors;
using RedditPodcastPoster.UrlSubmission.Tests.Support;
using Xunit;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class CatalogueCreationIdentityRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<Film> _films = [];
    private CatalogueIdentityAdmission _filmAdmission = CatalogueIdentityAdmission.Proceed();

    public CatalogueCreationIdentityRules()
    {
        _mocker.Use<TimeProvider>(TimeProvider.System);
        _mocker.Use(Options.Create(new SubmitContentTypesOptions { Enabled = true }));
        _mocker.Use<INonPodcastServiceAdapterResolver>(NonPodcastSubmitAdapterResolverSupport.Create());
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(x => x.ResolveFilmAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<CatalogueIdentityChoice>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _filmAdmission);
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(x => x.ApplyMissing(It.IsAny<Film>(), It.IsAny<CatalogueAuthorityIds?>()))
            .Returns((Film film, CatalogueAuthorityIds? ids) => ids?.FillMissing(film) ?? false);
        _mocker.GetMock<IFilmRepository>()
            .Setup(x => x.GetBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .ReturnsAsync((Expression<Func<Film, bool>> predicate) => _films.FirstOrDefault(predicate.Compile()));
        _mocker.GetMock<IFilmRepository>()
            .Setup(x => x.Save(It.IsAny<Film>()))
            .Callback<Film>(_films.Add)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<ITvShowRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .Returns(Empty<TvShow>());
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(x => x.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync((TvShowEpisode?)null);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<NewsOrganisation, bool>>>()))
            .Returns(Empty<NewsOrganisation>());
        _mocker.GetMock<INewsReportRepository>()
            .Setup(x => x.GetBy(It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .ReturnsAsync((NewsReport?)null);
    }

    [Fact(DisplayName =
        "When authority search finds one film, submit stores the looked-up IMDb and TMDB ids on the new Film.")]
    public async Task unique_identity_is_stored_on_film_create()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var tmdbId = CreateTmdbId();
        var imdbId = ImdbId();
        _filmAdmission = CatalogueIdentityAdmission.Proceed(
            new CatalogueAuthorityIds(imdbId, tmdbId, null));
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(Film(title), FilmSubmitOptions());

        // Assert
        result!.EpisodeResult.Should().Be(SubmitResultState.Created);
        result.IdentityHold.Should().BeNull();
        _films.Should().ContainSingle();
        _films[0].ImdbId.Should().Be(imdbId);
        _films[0].TmdbId.Should().Be(tmdbId);
    }

    [Fact(DisplayName =
        "When authority search finds several films and the curator has not answered, nothing is created and the identity hold lists the candidates.")]
    public async Task ambiguous_identity_does_not_create_until_answered()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var firstId = CreateTmdbId();
        var secondId = CreateDistinctTmdbId(firstId);
        _filmAdmission = CatalogueIdentityAdmission.NeedsPrompt(
            new CatalogueIdentityHold(
                CatalogueAuthorityCandidate.FilmKind,
                title,
                [
                    new CatalogueAuthorityCandidate(
                        CatalogueAuthorityCandidate.TmdbKey(firstId),
                        _fixture.CreateTitle(),
                        Year: null,
                        CatalogueAuthorityCandidate.FilmKind,
                        CatalogueAuthorityCandidate.TmdbSource,
                        firstId,
                        TvdbId: null),
                    new CatalogueAuthorityCandidate(
                        CatalogueAuthorityCandidate.TmdbKey(secondId),
                        _fixture.CreateTitle(),
                        Year: null,
                        CatalogueAuthorityCandidate.FilmKind,
                        CatalogueAuthorityCandidate.TmdbSource,
                        secondId,
                        TvdbId: null)
                ]));
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(Film(title), FilmSubmitOptions());

        // Assert
        _films.Should().BeEmpty();
        result!.IdentityHold.Should().NotBeNull();
        result.IdentityHold!.Candidates.Should().HaveCount(2);
        result.EpisodeResult.Should().Be(SubmitResultState.None);
    }

    [Fact(DisplayName =
        "When the curator continues without identity after an ambiguous film match, the film is created with blank IMDb and TMDB ids.")]
    public async Task continue_creates_film_without_identity()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        _filmAdmission = CatalogueIdentityAdmission.Proceed();
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(
            Film(title),
            FilmSubmitOptions() with { IdentityChoice = CatalogueIdentityChoice.Continue });

        // Assert
        result!.EpisodeResult.Should().Be(SubmitResultState.Created);
        _films.Should().ContainSingle();
        _films[0].ImdbId.Should().BeNull();
        _films[0].TmdbId.Should().BeNull();
    }

    private SubmitOptions FilmSubmitOptions() =>
        new(
            null,
            MatchOtherServices: false,
            PersistToDatabase: true,
            ClassificationSignals: new SubmitClassificationSignals(
                new Uri($"https://www.netflix.com/watch/{_fixture.CreateGuid():N}"),
                MadeAsFilm: true));

    private CategorisedItem Film(string title) =>
        new(
            null,
            null,
            null,
            null,
            null,
            null,
            new ResolvedNonPodcastServiceItem(
                StreamingService.Netflix,
                Url: new Uri($"https://www.netflix.com/watch/{_fixture.CreateGuid():N}"),
                Title: title,
                Description: _fixture.Create<string>(),
                MadeAsFilm: true),
            Service.Other);

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

    private static async IAsyncEnumerable<T> Empty<T>()
    {
        await Task.CompletedTask;
        yield break;
    }
}
