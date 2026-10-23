using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Heroes;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Factories;
using RedditPodcastPoster.UrlSubmission.Languages;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Processors;
using RedditPodcastPoster.UrlSubmission.Tests.Support;
using Xunit;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

/// <summary>
/// Language is decided before a new film or podcast is stored. Spanish is already supported.
/// Icelandic is a real neutral culture that the default supported list does not include.
/// </summary>
public class CatalogueCreationLanguageRules
{
    private const string Spanish =
        "el programa de la noche con una historia para todos";

    private const string Icelandic =
        "að ekki það þetta fyrir og ekki það";

    private static readonly string[] Supported = ["es", "fr"];

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<string> _registered = [];
    private readonly List<Film> _films = [];

    public CatalogueCreationLanguageRules()
    {
        var registrar = _mocker.GetMock<ISupportedLanguageRegistrar>();
        registrar.Setup(r => r.RegisterAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((name, _) => _registered.Add(name))
            .Returns(Task.CompletedTask);
        _mocker.Use(Options.Create(new SubmitContentTypesOptions { Enabled = true }));
        _mocker.Use<INonPodcastServiceAdapterResolver>(NonPodcastSubmitAdapterResolverSupport.Create());
        _mocker.Use<ICatalogueLanguageGate>(new CatalogueLanguageGate(new CatalogueLanguageDetector(), registrar.Object));
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(x => x.ResolveFilmAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<CatalogueIdentityChoice>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CatalogueIdentityAdmission.Proceed());
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(x => x.ResolveTvShowEpisodeAsync(
                It.IsAny<TvShow>(),
                It.IsAny<string>(),
                It.IsAny<CatalogueIdentityChoice>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CatalogueIdentityAdmission.Proceed());
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
        "When detection finds a supported non-English language, creating a film stores that language as its default and reports that the default was set.")]
    public async Task supported_language_is_set_on_create()
    {
        // Arrange
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(Film(Spanish), GateOptions(CatalogueLanguageChoice.Unset));

        // Assert
        result!.EpisodeResult.Should().Be(SubmitResultState.Created);
        result.DefaultLanguageFromDetection.Should().BeTrue();
        _films.Should().ContainSingle();
        _films[0].Language.Should().Be("es");
        _registered.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When detection finds a language outside the supported list and the user has not answered, nothing is created and the prompt names the language and the fields that led there.")]
    public async Task unsupported_language_does_not_create_until_the_user_answers()
    {
        // Arrange
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(Film(Icelandic), GateOptions(CatalogueLanguageChoice.Unset));

        // Assert
        _films.Should().BeEmpty();
        result!.LanguageHold.Should().NotBeNull();
        result.LanguageHold!.Code.Should().Be("is");
        result.LanguageHold.Name.Should().Be("Icelandic");
        result.LanguageHold.Signals.Should().NotBeEmpty();
        result.LanguageHold.Signals.Should().Contain(signal =>
            signal.Field == "parentTitle" || signal.Field == "episodeTitle");
        _registered.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the user adds an unsupported language, that language is registered and the new film is then created with it as the default language.")]
    public async Task yes_adds_the_language_then_creates_with_that_default()
    {
        // Arrange
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(Film(Icelandic), GateOptions(CatalogueLanguageChoice.Add));

        // Assert
        _registered.Should().Equal("Icelandic");
        result!.DefaultLanguageFromDetection.Should().BeTrue();
        _films.Should().ContainSingle();
        _films[0].Language.Should().Be("is");
    }

    [Fact(DisplayName =
        "When the user cancels an unsupported language, the film is not created and the language is not added.")]
    public async Task cancel_creates_nothing()
    {
        // Arrange
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(Film(Icelandic), GateOptions(CatalogueLanguageChoice.Cancel));

        // Assert
        _films.Should().BeEmpty();
        result!.EpisodeResult.Should().Be(SubmitResultState.None);
        result.LanguageHold.Should().NotBeNull();
        _registered.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the user continues without adding an unsupported language, the film is created with no default language and the default-language flag stays off.")]
    public async Task continue_creates_without_a_default_language()
    {
        // Arrange
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(Film(Icelandic), GateOptions(CatalogueLanguageChoice.Continue));

        // Assert
        result!.EpisodeResult.Should().Be(SubmitResultState.Created);
        result.DefaultLanguageFromDetection.Should().BeFalse();
        _films.Should().ContainSingle();
        _films[0].Language.Should().BeNull();
        _registered.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A language probe of a new film does not save it, because detection has to finish before any catalogue row is created.")]
    public async Task probe_does_not_create_the_film()
    {
        // Arrange
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();
        var options = GateOptions(CatalogueLanguageChoice.Unset) with
        {
            PersistToDatabase = false,
            LanguageProbe = true
        };

        // Act
        var result = await sut.TrySubmit(Film(Spanish), options);

        // Assert
        _films.Should().BeEmpty();
        result!.EpisodeResult.Should().Be(SubmitResultState.None);
        result.DefaultLanguageFromDetection.Should().BeTrue();
    }

    private SubmitOptions GateOptions(CatalogueLanguageChoice choice) =>
        new(
            null,
            MatchOtherServices: false,
            PersistToDatabase: true,
            ClassificationSignals: new SubmitClassificationSignals(
                new Uri($"https://www.netflix.com/watch/{_fixture.CreateGuid():N}"),
                MadeAsFilm: true),
            EnforceLanguageGate: true,
            SupportedLanguageCodes: Supported,
            LanguageChoice: choice);

    private CategorisedItem Film(string text) =>
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
                Title: text,
                Description: text,
                MadeAsFilm: true),
            Service.Other);

    private static async IAsyncEnumerable<T> Empty<T>()
    {
        await Task.CompletedTask;
        yield break;
    }
}

public class CataloguePodcastCreationLanguageRules
{
    private const string Spanish =
        "el programa de la noche con una historia para todos";

    private const string Icelandic =
        "að ekki það þetta fyrir og ekki það";

    private static readonly string[] Supported = ["es", "fr"];

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<string> _registered = [];
    private readonly List<Podcast> _savedPodcasts = [];

    public CataloguePodcastCreationLanguageRules()
    {
        var registrar = _mocker.GetMock<ISupportedLanguageRegistrar>();
        registrar.Setup(r => r.RegisterAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((name, _) => _registered.Add(name))
            .Returns(Task.CompletedTask);
        _mocker.Use<ICatalogueLanguageGate>(new CatalogueLanguageGate(new CatalogueLanguageDetector(), registrar.Object));
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.Save(It.IsAny<Podcast>()))
            .Callback<Podcast>(_savedPodcasts.Add)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.Save(It.IsAny<Episode>()))
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IHeroEpisodePromoter>()
            .Setup(p => p.PromoteAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mocker.GetMock<ICatalogueKindSubmitter>()
            .Setup(s => s.TrySubmit(It.IsAny<CategorisedItem>(), It.IsAny<SubmitOptions>()))
            .ReturnsAsync((SubmitResult?)null);
    }

    [Fact(DisplayName =
        "When a new podcast's text is a supported non-English language, the create receives that language and the podcast is saved with the default-language flag set.")]
    public async Task supported_language_is_passed_into_podcast_create()
    {
        // Arrange
        string? language = null;
        var podcast = Podcast();
        ArrangeCreate(podcast, captured => language = captured);
        var sut = _mocker.CreateInstance<CategorisedItemProcessor>();

        // Act
        var result = await sut.ProcessCategorisedItem(Item(Spanish), GateOptions(CatalogueLanguageChoice.Unset));

        // Assert
        language.Should().Be("es");
        result.DefaultLanguageFromDetection.Should().BeTrue();
        _savedPodcasts.Should().ContainSingle().Which.Should().BeSameAs(podcast);
        _registered.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a new podcast's text is outside the supported list and the user has not answered, the podcast and episode are not saved.")]
    public async Task unsupported_language_does_not_save_a_podcast()
    {
        // Arrange
        var sut = _mocker.CreateInstance<CategorisedItemProcessor>();

        // Act
        var result = await sut.ProcessCategorisedItem(Item(Icelandic), GateOptions(CatalogueLanguageChoice.Unset));

        // Assert
        _savedPodcasts.Should().BeEmpty();
        result.LanguageHold!.Code.Should().Be("is");
        _mocker.GetMock<IPodcastAndEpisodeFactory>().Verify(
            f => f.CreatePodcastWithEpisode(It.IsAny<CategorisedItem>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When the user adds the language for a new podcast, the language is registered and the create receives that code as the default.")]
    public async Task yes_adds_the_language_then_creates_the_podcast_with_that_default()
    {
        // Arrange
        string? language = null;
        ArrangeCreate(Podcast(), captured => language = captured);
        var sut = _mocker.CreateInstance<CategorisedItemProcessor>();

        // Act
        var result = await sut.ProcessCategorisedItem(Item(Icelandic), GateOptions(CatalogueLanguageChoice.Add));

        // Assert
        _registered.Should().Equal("Icelandic");
        language.Should().Be("is");
        result.DefaultLanguageFromDetection.Should().BeTrue();
        _savedPodcasts.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "When the user cancels an unsupported language on a new podcast, nothing is saved and the language is not added.")]
    public async Task cancel_does_not_create_the_podcast()
    {
        // Arrange
        var sut = _mocker.CreateInstance<CategorisedItemProcessor>();

        // Act
        var result = await sut.ProcessCategorisedItem(Item(Icelandic), GateOptions(CatalogueLanguageChoice.Cancel));

        // Assert
        _savedPodcasts.Should().BeEmpty();
        result.EpisodeResult.Should().Be(SubmitResultState.None);
        _registered.Should().BeEmpty();
        _mocker.GetMock<IPodcastAndEpisodeFactory>().Verify(
            f => f.CreatePodcastWithEpisode(It.IsAny<CategorisedItem>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When the user continues without a default language, the podcast is created with no language code and the default-language flag stays off.")]
    public async Task continue_creates_the_podcast_without_a_default_language()
    {
        // Arrange
        string? language = "sentinel";
        ArrangeCreate(Podcast(), captured => language = captured);
        var sut = _mocker.CreateInstance<CategorisedItemProcessor>();

        // Act
        var result = await sut.ProcessCategorisedItem(Item(Icelandic), GateOptions(CatalogueLanguageChoice.Continue));

        // Assert
        language.Should().BeNull();
        result.DefaultLanguageFromDetection.Should().BeFalse();
        _savedPodcasts.Should().ContainSingle();
        _registered.Should().BeEmpty();
    }

    private void ArrangeCreate(Podcast podcast, Action<string?> captureLanguage)
    {
        var episode = _fixture.CreateStoredEpisode(podcast);
        _mocker.GetMock<IPodcastAndEpisodeFactory>()
            .Setup(f => f.CreatePodcastWithEpisode(
                It.IsAny<CategorisedItem>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .Callback<CategorisedItem, string?, string?>((_, _, language) => captureLanguage(language))
            .ReturnsAsync(new CreatePodcastWithEpisodeResponse(podcast, episode, new SubmitEpisodeDetails(false, false, false)));
    }

    private Podcast Podcast()
    {
        var podcast = _fixture.CreatePodcast();
        podcast.AlwaysPromoteAsHero = false;
        podcast.Language = null;
        return podcast;
    }

    private SubmitOptions GateOptions(CatalogueLanguageChoice choice) =>
        new(
            null,
            MatchOtherServices: false,
            EnforceLanguageGate: true,
            SupportedLanguageCodes: Supported,
            LanguageChoice: choice);

    private CategorisedItem Item(string text) =>
        new(
            null,
            null,
            null,
            new CategorisedSpotifyItem(
                _fixture.CreateSpotifyId(),
                _fixture.CreateSpotifyId(),
                text,
                text,
                _fixture.Create<string>(),
                text,
                text,
                DomainTestFixture.UtcToday,
                _fixture.CreateDuration(),
                null,
                false,
                null),
            null,
            null,
            null,
            Service.Spotify);
}
