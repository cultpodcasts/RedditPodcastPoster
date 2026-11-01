using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Episodes.Logging;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Adaptors;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Processors;
using RedditPodcastPoster.UrlSubmission.Services;
using RedditPodcastPoster.UrlSubmission.Submitters;
using RedditPodcastPoster.UrlSubmission.Tests.Support;
using Xunit;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

/// <summary>
/// Discovery submits (curator submit and supervisor apply) derive the same classification signals as the toolbar
/// submit, so a discovered film or TV show episode is kind-classified and its result carries what the curation host
/// records and indexes. Every rule runs through the real <see cref="DiscoveryUrlSubmitter"/> and
/// <see cref="DiscoveryResultProcessor"/>; the categoriser output and all ids and names are synthetic.
/// </summary>
public class DiscoveryKindClassificationRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<Film> _films = [];
    private readonly List<TvShow> _shows = [];
    private readonly List<TvShowEpisode> _tvEpisodes = [];
    private readonly List<SubmitOptions> _processedOptions = [];

    public DiscoveryKindClassificationRules()
    {
        _mocker.Use<TimeProvider>(TimeProvider.System);
        _mocker.Use<ISubmitResultAdaptor>(new SubmitResultAdaptor());
        _mocker.Use<INonPodcastServiceAdapterResolver>(NonPodcastSubmitAdapterResolverSupport.Create());
        // No podcast, news organisation or TV show owns the discovered channel: the podcast pipeline runs.
        _mocker.GetMock<IEpisodeUrlRouter>()
            .Setup(r => r.RouteByOwner(It.IsAny<Uri>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(new EpisodeUrlRoute(null, null, null));
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(x => x.ResolveFilmAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CatalogueIdentityChoice>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CatalogueIdentityAdmission.Proceed());
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(x => x.ResolveTvShowEpisodeAsync(It.IsAny<TvShow>(), It.IsAny<string>(),
                It.IsAny<CatalogueIdentityChoice>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CatalogueIdentityAdmission.Proceed());
        _mocker.GetMock<IFileKeyAllocator>()
            .Setup(x => x.Allocate(It.IsAny<FileKeyKind>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string?>()))
            .ReturnsAsync((FileKeyKind _, string _, Guid id, string? _) => $"synthetic-{id:N}");
        _mocker.GetMock<ICatalogueSubmitEnrichment>()
            .Setup(x => x.Enrich(It.IsAny<Film>(), It.IsAny<CategorisedItem>()))
            .ReturnsAsync(new SubmitEpisodeDetails(false, false, false));
        _mocker.GetMock<ICatalogueSubmitEnrichment>()
            .Setup(x => x.Enrich(It.IsAny<Playable>(), It.IsAny<Publisher>(), It.IsAny<CatalogueIndexingSource?>(),
                It.IsAny<CategorisedItem>()))
            .ReturnsAsync(new SubmitEpisodeDetails(false, false, false));
        _mocker.GetMock<IFilmRepository>()
            .Setup(x => x.GetBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .ReturnsAsync((Expression<Func<Film, bool>> p) => _films.FirstOrDefault(p.Compile()));
        _mocker.GetMock<IFilmRepository>()
            .Setup(x => x.Save(It.IsAny<Film>()))
            .Callback<Film>(_films.Add)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<ITvShowRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .Returns((Expression<Func<TvShow, bool>> p) => ToAsync(_shows.Where(p.Compile())));
        _mocker.GetMock<ITvShowRepository>()
            .Setup(x => x.Save(It.IsAny<TvShow>()))
            .Callback<TvShow>(_shows.Add)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(x => x.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync((Expression<Func<TvShowEpisode, bool>> p) => _tvEpisodes.FirstOrDefault(p.Compile()));
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(x => x.Save(It.IsAny<TvShowEpisode>()))
            .Callback<TvShowEpisode>(_tvEpisodes.Add)
            .Returns(Task.CompletedTask);
    }

    private Uri YouTubeUrl() => new($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}");

    private Uri SpotifyUrl() => new($"https://open.spotify.com/episode/{_fixture.CreateSpotifyId()}");

    private Uri TubiUrl() => new($"https://tubitv.com/movies/{Math.Abs(_fixture.Create<int>())}/synthetic");

    private static DiscoveryResult Result(Uri? youTube = null, Uri? spotify = null) =>
        new() { Id = Guid.NewGuid(), Urls = new DiscoveryResultUrls { YouTube = youTube, Spotify = spotify } };

    private static SubmitOptions DiscoveryOptions() => new(null, true, CreationSource: EpisodeCreationSource.Discovery);

    private void UseFlag(bool enabled) =>
        _mocker.Use(Options.Create(new SubmitContentTypesOptions { Enabled = enabled }));

    /// <summary>The real categorised-item processor and kind submitter behind the discovery processor.</summary>
    private void UseRealPipeline()
    {
        _mocker.Use<ICatalogueKindSubmitter>(_mocker.CreateInstance<CatalogueKindSubmitter>());
        _mocker.Use<ICategorisedItemProcessor>(_mocker.CreateInstance<CategorisedItemProcessor>());
        _mocker.Use<IDiscoveryResultProcessor>(_mocker.CreateInstance<DiscoveryResultProcessor>());
    }

    /// <summary>Records the options the discovery processor hands on, and returns <paramref name="result"/>.</summary>
    private void UseRecordingProcessor(SubmitResult result)
    {
        _mocker.GetMock<ICategorisedItemProcessor>()
            .Setup(p => p.ProcessCategorisedItem(It.IsAny<CategorisedItem>(), It.IsAny<SubmitOptions>()))
            .Callback<CategorisedItem, SubmitOptions>((_, options) => _processedOptions.Add(options))
            .ReturnsAsync(result);
        _mocker.Use<IDiscoveryResultProcessor>(_mocker.CreateInstance<DiscoveryResultProcessor>());
    }

    private void CategoriserReturns(Uri url, CategorisedItem item) =>
        _mocker.GetMock<IUrlCategoriser>()
            .Setup(c => c.Categorise(It.IsAny<Podcast?>(), url, It.IsAny<IndexingContext>(), It.IsAny<bool>(),
                It.IsAny<NonPodcastServiceItemMetaData?>(), It.IsAny<bool>()))
            .ReturnsAsync(item);

    private static CategorisedItem NonPodcastItem(ResolvedNonPodcastServiceItem source) =>
        new(null, null, null, null, null, null, source, Service.Other);

    private ResolvedNonPodcastServiceItem FilmSource() =>
        new(StreamingService.Tubi, Url: TubiUrl(), Title: _fixture.CreateTitle(),
            Description: _fixture.Create<string>(), MadeAsFilm: true);

    private ResolvedNonPodcastServiceItem SeriesSource() =>
        FilmSource() with { ShowName = _fixture.CreateTitle(), MadeAsFilm = false };

    [Fact(DisplayName =
        "With content types on, a discovered item the categoriser resolves as made-as-film is stored as a Film and the result carries the Film kind, its id and its search source with no podcast episode, because the curation host records and indexes films directly.")]
    public async Task made_as_film_discovery_creates_film()
    {
        // Arrange
        UseFlag(true);
        var url = YouTubeUrl();
        CategoriserReturns(url, NonPodcastItem(FilmSource()));
        UseRealPipeline();
        var sut = _mocker.CreateInstance<DiscoveryUrlSubmitter>();

        // Act
        var result = await sut.Submit(Result(youTube: url), new IndexingContext(), DiscoveryOptions());

        // Assert
        var film = _films.Should().ContainSingle().Subject;
        result.State.Should().Be(DiscoverySubmitResultState.CreatedEpisode);
        result.Episode.Should().BeNull();
        result.ContentKind.Should().Be(SubmitClassification.Film);
        result.PlayableId.Should().Be(film.Id);
        result.ParentId.Should().BeNull();
        result.FilmSearchSource.Should().BeSameAs(film);
        result.CatalogueSearchSource.Should().BeNull();
    }

    [Fact(DisplayName =
        "With content types on, a discovered item the categoriser resolves with a series name is stored as a TV show episode under a new TV show, and the result carries the TvShowEpisode kind, the episode id, the show id and the search source, because the host indexes it at submit.")]
    public async Task series_discovery_creates_tv_show_episode()
    {
        // Arrange
        UseFlag(true);
        var url = SpotifyUrl();
        var source = SeriesSource();
        CategoriserReturns(url, NonPodcastItem(source));
        UseRealPipeline();
        var sut = _mocker.CreateInstance<DiscoveryUrlSubmitter>();

        // Act
        var result = await sut.Submit(Result(spotify: url), new IndexingContext(), DiscoveryOptions());

        // Assert
        var show = _shows.Should().ContainSingle().Subject;
        show.Name.Should().Be(source.ShowName);
        var episode = _tvEpisodes.Should().ContainSingle().Subject;
        result.State.Should().Be(DiscoverySubmitResultState.CreatedPodcastAndEpisode);
        result.Episode.Should().BeNull();
        result.ContentKind.Should().Be(SubmitClassification.TvShowEpisode);
        result.PlayableId.Should().Be(episode.Id);
        result.ParentId.Should().Be(show.Id);
        result.CatalogueSearchSource.Should().NotBeNull();
        result.CatalogueSearchSource!.Kind.Should().Be(CatalogueParentKind.TvShow);
        result.FilmSearchSource.Should().BeNull();
    }

    [Fact(DisplayName =
        "Re-submitting a discovered film that is already stored reports EpisodeAlreadyExists with the Film kind and its id and stores no second film, so a retry is a no-op.")]
    public async Task stored_film_is_reported_as_already_existing()
    {
        // Arrange
        UseFlag(true);
        var url = YouTubeUrl();
        CategoriserReturns(url, NonPodcastItem(FilmSource()));
        UseRealPipeline();
        var sut = _mocker.CreateInstance<DiscoveryUrlSubmitter>();
        await sut.Submit(Result(youTube: url), new IndexingContext(), DiscoveryOptions());

        // Act
        var result = await sut.Submit(Result(youTube: url), new IndexingContext(), DiscoveryOptions());

        // Assert
        var film = _films.Should().ContainSingle().Subject;
        result.State.Should().Be(DiscoverySubmitResultState.EpisodeAlreadyExists);
        result.ContentKind.Should().Be(SubmitClassification.Film);
        result.PlayableId.Should().Be(film.Id);
        result.FilmSearchSource.Should().BeNull();
    }

    [Fact(DisplayName =
        "With content types on, a discovered Spotify podcast episode is given podcast-service signals for its own URL and stays a podcast episode with a null content kind, so the host still indexes it through the episodes container.")]
    public async Task podcast_episode_keeps_podcast_path()
    {
        // Arrange
        UseFlag(true);
        var url = SpotifyUrl();
        CategoriserReturns(url, new CategorisedItem(null, null, null, null, null, null, null, Service.Spotify));
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        UseRecordingProcessor(new SubmitResult(SubmitResultState.Created, SubmitResultState.None, Episode: episode));
        var sut = _mocker.CreateInstance<DiscoveryUrlSubmitter>();

        // Act
        var result = await sut.Submit(Result(spotify: url), new IndexingContext(), DiscoveryOptions());

        // Assert
        var signals = _processedOptions.Should().ContainSingle().Subject.ClassificationSignals;
        signals.Should().NotBeNull();
        signals!.Url.Should().Be(url);
        signals.PodcastServiceEpisode.Should().BeTrue();
        signals.MadeAsFilm.Should().BeFalse();
        signals.Series.Should().BeFalse();
        result.State.Should().Be(DiscoverySubmitResultState.CreatedEpisode);
        result.Episode.Should().BeSameAs(episode);
        result.ContentKind.Should().BeNull();
        result.PlayableId.Should().BeNull();
        result.CatalogueSearchSource.Should().BeNull();
        result.FilmSearchSource.Should().BeNull();
    }

    [Fact(DisplayName =
        "With content types off, discovery sets no classification signals, so a discovered item keeps today's podcast-episode path.")]
    public async Task flag_off_sets_no_signals()
    {
        // Arrange
        UseFlag(false);
        var url = YouTubeUrl();
        CategoriserReturns(url, NonPodcastItem(FilmSource()));
        var podcast = _fixture.CreatePodcast();
        UseRecordingProcessor(new SubmitResult(SubmitResultState.Created, SubmitResultState.Created,
            Episode: _fixture.CreateStoredEpisode(podcast)));
        var sut = _mocker.CreateInstance<DiscoveryUrlSubmitter>();

        // Act
        var result = await sut.Submit(Result(youTube: url), new IndexingContext(), DiscoveryOptions());

        // Assert
        _processedOptions.Should().ContainSingle().Which.ClassificationSignals.Should().BeNull();
        result.State.Should().Be(DiscoverySubmitResultState.CreatedPodcastAndEpisode);
        result.ContentKind.Should().BeNull();
    }

    [Theory(DisplayName =
        "A discovered item the classifier rejects or holds for a curator fails with a reason rather than an unknown-state error, so the curation host marks it errored with a readable message.")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task rejected_or_curator_items_fail_with_reason(bool rejected, bool requiresCurator)
    {
        // Arrange
        UseFlag(true);
        var url = YouTubeUrl();
        CategoriserReturns(url, NonPodcastItem(FilmSource()));
        UseRecordingProcessor(new SubmitResult(SubmitResultState.None, SubmitResultState.None,
            ContentKind: SubmitClassification.Episode, Rejected: rejected, RequiresCurator: requiresCurator));
        var sut = _mocker.CreateInstance<DiscoveryUrlSubmitter>();

        // Act
        var act = () => sut.Submit(Result(youTube: url), new IndexingContext(), DiscoveryOptions());

        // Assert
        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Message.Should().Contain(rejected ? "not accepted" : "curator");
    }

    [Fact(DisplayName =
        "Signals a caller already supplied are kept, so discovery never overrides an explicit classification.")]
    public async Task caller_signals_are_kept()
    {
        // Arrange
        UseFlag(true);
        var url = YouTubeUrl();
        CategoriserReturns(url, NonPodcastItem(FilmSource()));
        var podcast = _fixture.CreatePodcast();
        UseRecordingProcessor(new SubmitResult(SubmitResultState.Created, SubmitResultState.None,
            Episode: _fixture.CreateStoredEpisode(podcast)));
        var supplied = new SubmitClassificationSignals(url, News: true);
        var sut = _mocker.CreateInstance<DiscoveryUrlSubmitter>();

        // Act
        await sut.Submit(Result(youTube: url), new IndexingContext(), DiscoveryOptions() with { ClassificationSignals = supplied });

        // Assert
        _processedOptions.Should().ContainSingle().Which.ClassificationSignals.Should().BeSameAs(supplied);
    }

    private static async IAsyncEnumerable<T> ToAsync<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }
}
