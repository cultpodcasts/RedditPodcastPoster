using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Categorisers;
using RedditPodcastPoster.PodcastServices.Handlers;
using RedditPodcastPoster.PodcastServices.Tests.Support;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.NonPodcast;

public class NonPodcastServiceCategoriserRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly InMemoryEpisodeRepository _episodes = new();
    private readonly InMemoryPodcastRepository _podcasts = new();
    private ResolvedNonPodcastServiceItem? _handlerResult;
    private Podcast? _handlerPodcast;
    private IReadOnlyList<Episode>? _handlerEpisodes;

    public NonPodcastServiceCategoriserRules()
    {
        _mocker.Use<IEpisodeRepository>(_episodes);
        _mocker.Use<IPodcastRepository>(_podcasts);
        _mocker.Use<INonPodcastServiceAdapterResolver>(
            NonPodcastSubmitAdapterResolverSupport.CreateMocks());
        _mocker.GetMock<IStreamingServiceMetaDataHandler>()
            .Setup(x => x.ResolveServiceItem(
                It.IsAny<Podcast?>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<Uri>()))
            .ReturnsAsync((Podcast? podcast, IEnumerable<Episode> episodes, Uri _) =>
            {
                _handlerPodcast = podcast;
                _handlerEpisodes = episodes.ToList();
                return _handlerResult!;
            });
    }

    [Fact(DisplayName =
        "When no podcast is supplied and no stored episode has the Sounds URL, " +
        "submit extracts metadata with an empty episode list so a new episode (and series) can be created.")]
    public async Task missing_podcast_with_unknown_sounds_url_extracts_metadata()
    {
        // Arrange
        var url = BbcSoundsUrl();
        _handlerResult = CreateResolved(NonPodcastService.BBC, url);
        var sut = _mocker.CreateInstance<NonPodcastServiceCategoriser>();

        // Act
        var resolved = await sut.Resolve(null, url, new IndexingContext());

        // Assert
        resolved.Should().BeSameAs(_handlerResult);
        _handlerPodcast.Should().BeNull();
        _handlerEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When no podcast is supplied but one stored episode already has the Sounds URL, " +
        "submit returns that episode and podcast without extracting metadata again.")]
    public async Task missing_podcast_with_existing_sounds_url_returns_stored_episode()
    {
        // Arrange
        var url = BbcSoundsUrl();
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast, e => SeedBbcSoundsLookup(e, url));
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<NonPodcastServiceCategoriser>();

        // Act
        var resolved = await sut.Resolve(null, url, new IndexingContext());

        // Assert
        resolved.Should().NotBeNull();
        resolved!.NonPodcastService.Should().Be(NonPodcastService.BBC);
        resolved.Podcast!.Id.Should().Be(podcast.Id);
        resolved.Episode!.Id.Should().Be(episode.Id);
        _mocker.GetMock<IStreamingServiceMetaDataHandler>()
            .Verify(
                x => x.ResolveServiceItem(
                    It.IsAny<Podcast?>(),
                    It.IsAny<IEnumerable<Episode>>(),
                    It.IsAny<Uri>()),
                Times.Never);
    }

    [Fact(DisplayName =
        "When no podcast is supplied but one stored episode already has the Internet Archive URL, " +
        "submit returns that episode tagged Internet Archive.")]
    public async Task missing_podcast_with_existing_archive_url_returns_stored_episode()
    {
        // Arrange
        var url = InternetArchiveUrl();
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
            EpisodeServicePresence.Upsert(e, ServiceKeys.InternetArchive, url, null));
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<NonPodcastServiceCategoriser>();

        // Act
        var resolved = await sut.Resolve(null, url, new IndexingContext());

        // Assert
        resolved!.NonPodcastService.Should().Be(NonPodcastService.InternetArchive);
        resolved.Episode!.Id.Should().Be(episode.Id);
    }

    [Fact(DisplayName =
        "When a podcast is supplied, submit loads that podcast's episodes and extracts metadata for the URL, " +
        "so a recognised extra-service link can be attached to a chosen series.")]
    public async Task supplied_podcast_extracts_against_that_series_episodes()
    {
        // Arrange
        var url = BbcSoundsUrl();
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        _episodes.Seed(episode);
        _handlerResult = CreateResolved(NonPodcastService.BBC, url, podcast, episode);
        var sut = _mocker.CreateInstance<NonPodcastServiceCategoriser>();

        // Act
        var resolved = await sut.Resolve(podcast, url, new IndexingContext());

        // Assert
        resolved.Should().BeSameAs(_handlerResult);
        _handlerPodcast.Should().BeSameAs(podcast);
        _handlerEpisodes.Should().ContainSingle(e => e.Id == episode.Id);
    }

    [Fact(DisplayName =
        "When no podcast is supplied and the URL host has no adapter (example.test), " +
        "submit fails because the categoriser does not recognise the service.")]
    public async Task unknown_service_without_podcast_is_rejected()
    {
        // Arrange
        var url = new Uri($"https://example.test/watch/{_fixture.CreateYouTubeId()}");
        var sut = _mocker.CreateInstance<NonPodcastServiceCategoriser>();

        // Act
        var act = async () => await sut.Resolve(null, url, new IndexingContext());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Unrecognised service*");
    }

    [Fact(DisplayName =
        "When no podcast is supplied and the URL is a Vimeo submit URL, " +
        "submit reaches the metadata handler instead of rejecting the service as unrecognised.")]
    public async Task missing_podcast_with_vimeo_url_extracts_metadata()
    {
        // Arrange
        var url = new Uri($"https://vimeo.com/{_fixture.CreateAppleId()}");
        _handlerResult = CreateResolved(NonPodcastService.Vimeo, url);
        var sut = _mocker.CreateInstance<NonPodcastServiceCategoriser>();

        // Act
        var resolved = await sut.Resolve(null, url, new IndexingContext());

        // Assert
        resolved.Should().BeSameAs(_handlerResult);
        _mocker.GetMock<IStreamingServiceMetaDataHandler>()
            .Verify(
                x => x.ResolveServiceItem(
                    It.IsAny<Podcast?>(),
                    It.IsAny<IEnumerable<Episode>>(),
                    url),
                Times.Once);
    }

    [Fact(DisplayName =
        "When no podcast is supplied and two podcasts already store the same Sounds URL, " +
        "submit fails because the URL is ambiguous across series.")]
    public async Task multiple_podcasts_for_same_url_is_rejected()
    {
        // Arrange
        var url = BbcSoundsUrl();
        var first = _fixture.CreatePodcast();
        var second = _fixture.CreatePodcast();
        _podcasts.Seed(first, second);
        _episodes.Seed(
            _fixture.CreateStoredEpisode(first, e => SeedBbcSoundsLookup(e, url)),
            _fixture.CreateStoredEpisode(second, e => SeedBbcSoundsLookup(e, url)));
        var sut = _mocker.CreateInstance<NonPodcastServiceCategoriser>();

        // Act
        var act = async () => await sut.Resolve(null, url, new IndexingContext());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{url}*");
    }

    private static void SeedBbcSoundsLookup(Episode episode, Uri soundsUrl)
    {
        episode.Services = new Dictionary<string, EpisodeServiceLink>(StringComparer.Ordinal)
        {
            [ServiceKeys.BbcIplayer] = new(),
            [ServiceKeys.BbcSounds] = new() { Url = soundsUrl }
        };
    }

    private ResolvedNonPodcastServiceItem CreateResolved(
        NonPodcastService service,
        Uri url,
        Podcast? podcast = null,
        Episode? episode = null) =>
        new(
            service,
            podcast,
            episode,
            url,
            _fixture.CreateTitle(),
            _fixture.Create<string>());

    private Uri BbcSoundsUrl() =>
        new($"https://www.bbc.co.uk/sounds/play/{_fixture.CreateYouTubeId()}");

    private Uri InternetArchiveUrl() =>
        new($"https://archive.org/details/{_fixture.CreateYouTubeId()}");
}
