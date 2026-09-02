using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.AmazonPrime.Extractors;
using RedditPodcastPoster.BBC.Extractors;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.InternetArchive.Extractors;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Netflix.Extractors;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Categorisers;
using RedditPodcastPoster.PodcastServices.Handlers;
using RedditPodcastPoster.PodcastServices.Tests.Support;
using RedditPodcastPoster.Vimeo.Extractors;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.NonPodcast;

public class StreamingServiceMetaDataHandlerRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private NonPodcastServiceItemMetaData? _bbcMeta;
    private NonPodcastServiceItemMetaData? _archiveMeta;
    private NonPodcastServiceItemMetaData? _vimeoMeta;
    private NonPodcastServiceItemMetaData? _netflixMeta;

    public StreamingServiceMetaDataHandlerRules()
    {
        _mocker.GetMock<IBBCPageMetaDataExtractor>()
            .Setup(x => x.GetMetaData(It.IsAny<Uri>()))
            .ReturnsAsync(() => _bbcMeta!);
        _mocker.GetMock<IInternetArchivePageMetaDataExtractor>()
            .Setup(x => x.GetMetaData(It.IsAny<Uri>()))
            .ReturnsAsync(() => _archiveMeta!);
        _mocker.GetMock<IVimeoMetaDataExtractor>()
            .Setup(x => x.GetMetaData(It.IsAny<Uri>()))
            .ReturnsAsync(() => _vimeoMeta!);
        _mocker.GetMock<INetflixPageMetaDataExtractor>()
            .Setup(x => x.GetMetaData(It.IsAny<Uri>()))
            .ReturnsAsync(() => _netflixMeta!);
        _mocker.Use<INonPodcastServiceAdapterResolver>(
            NonPodcastSubmitAdapterResolverSupport.Create(
                _mocker.GetMock<IBBCPageMetaDataExtractor>().Object,
                _mocker.GetMock<IInternetArchivePageMetaDataExtractor>().Object,
                _mocker.GetMock<IVimeoMetaDataExtractor>().Object,
                _mocker.GetMock<INetflixPageMetaDataExtractor>().Object,
                _mocker.GetMock<IAmazonPrimePageMetaDataExtractor>().Object));
    }

    [Fact(DisplayName =
        "When resolving a BBC Sounds play URL, metadata comes from the BBC extractor and the item is tagged BBC, " +
        "because Sounds is ingested as a non-podcast watch/listen page.")]
    public async Task sounds_url_uses_bbc_extractor_and_bbc_service()
    {
        // Arrange
        var url = BbcSoundsUrl();
        _bbcMeta = CreateMetaData();
        var sut = _mocker.CreateInstance<StreamingServiceMetaDataHandler>();

        // Act
        var resolved = await sut.ResolveServiceItem(null, [], url);

        // Assert
        resolved.NonPodcastService.Should().Be(NonPodcastService.BBC);
        resolved.Url.Should().Be(url);
        resolved.Title.Should().Be(_bbcMeta.Title);
        resolved.Description.Should().Be(_bbcMeta.Description);
        resolved.Duration.Should().Be(_bbcMeta.Duration);
        resolved.Release.Should().Be(_bbcMeta.Release);
        resolved.Image.Should().Be(_bbcMeta.Image);
        resolved.Publisher.Should().Be(_bbcMeta.Publisher);
        resolved.ShowName.Should().Be(_bbcMeta.ShowName);
        resolved.BBCUrl.Should().Be(url);
        resolved.InternetArchiveUrl.Should().BeNull();
        _mocker.GetMock<IInternetArchivePageMetaDataExtractor>()
            .Verify(x => x.GetMetaData(It.IsAny<Uri>()), Times.Never);
    }

    [Fact(DisplayName =
        "When resolving an Internet Archive details URL, metadata comes from the archive extractor and the item is tagged Internet Archive.")]
    public async Task archive_url_uses_internet_archive_extractor()
    {
        // Arrange
        var url = InternetArchiveUrl();
        _archiveMeta = CreateMetaData();
        var sut = _mocker.CreateInstance<StreamingServiceMetaDataHandler>();

        // Act
        var resolved = await sut.ResolveServiceItem(null, [], url);

        // Assert
        resolved.NonPodcastService.Should().Be(NonPodcastService.InternetArchive);
        resolved.Url.Should().Be(url);
        resolved.Title.Should().Be(_archiveMeta.Title);
        resolved.InternetArchiveUrl.Should().Be(url);
        resolved.BBCUrl.Should().BeNull();
        _mocker.GetMock<IBBCPageMetaDataExtractor>()
            .Verify(x => x.GetMetaData(It.IsAny<Uri>()), Times.Never);
    }

    [Fact(DisplayName =
        "When a podcast already has an episode whose Sounds URL matches the submitted URL, " +
        "that stored episode is returned as the matching episode so submit enriches instead of duplicating.")]
    public async Task matching_sounds_episode_is_returned_for_same_url()
    {
        // Arrange
        var url = BbcSoundsUrl();
        _bbcMeta = CreateMetaData();
        var podcast = _fixture.CreatePodcast();
        var matching = _fixture.CreateStoredEpisode(podcast, e =>
            EpisodeServicePresence.Upsert(e, ServiceKeys.BbcSounds, url, null));
        var other = _fixture.CreateStoredEpisode(podcast, e =>
            EpisodeServicePresence.Upsert(e, ServiceKeys.BbcSounds, BbcSoundsUrl(), null));
        var sut = _mocker.CreateInstance<StreamingServiceMetaDataHandler>();

        // Act
        var resolved = await sut.ResolveServiceItem(podcast, [matching, other], url);

        // Assert
        resolved.Episode.Should().NotBeNull();
        resolved.Episode!.Id.Should().Be(matching.Id);
        resolved.Podcast.Should().BeSameAs(podcast);
    }

    [Fact(DisplayName =
        "When a podcast already has an episode whose Internet Archive URL matches the submitted URL, " +
        "that stored episode is returned as the matching episode.")]
    public async Task matching_archive_episode_is_returned_for_same_url()
    {
        // Arrange
        var url = InternetArchiveUrl();
        _archiveMeta = CreateMetaData();
        var podcast = _fixture.CreatePodcast();
        var matching = _fixture.CreateStoredEpisode(podcast, e =>
            EpisodeServicePresence.Upsert(e, ServiceKeys.InternetArchive, url, null));
        var sut = _mocker.CreateInstance<StreamingServiceMetaDataHandler>();

        // Act
        var resolved = await sut.ResolveServiceItem(podcast, [matching], url);

        // Assert
        resolved.Episode!.Id.Should().Be(matching.Id);
    }

    [Fact(DisplayName =
        "When resolving a Vimeo video URL, metadata comes from the Vimeo extractor and the item is tagged Vimeo.")]
    public async Task vimeo_url_uses_vimeo_extractor()
    {
        // Arrange
        var url = new Uri($"https://vimeo.com/{_fixture.CreateAppleId()}");
        _vimeoMeta = CreateMetaData();
        var sut = _mocker.CreateInstance<StreamingServiceMetaDataHandler>();

        // Act
        var resolved = await sut.ResolveServiceItem(null, [], url);

        // Assert
        resolved.NonPodcastService.Should().Be(NonPodcastService.Vimeo);
        resolved.Url.Should().Be(url);
        resolved.Title.Should().Be(_vimeoMeta.Title);
        resolved.Image.Should().Be(_vimeoMeta.Image);
        _mocker.GetMock<IBBCPageMetaDataExtractor>()
            .Verify(x => x.GetMetaData(It.IsAny<Uri>()), Times.Never);
        _mocker.GetMock<INetflixPageMetaDataExtractor>()
            .Verify(x => x.GetMetaData(It.IsAny<Uri>()), Times.Never);
    }

    [Fact(DisplayName =
        "When resolving a Netflix title URL, metadata comes from the Netflix extractor and the item is tagged Netflix.")]
    public async Task netflix_url_uses_netflix_extractor()
    {
        // Arrange
        var url = new Uri($"https://www.netflix.com/title/{_fixture.CreateAppleId()}");
        _netflixMeta = CreateMetaData();
        var sut = _mocker.CreateInstance<StreamingServiceMetaDataHandler>();

        // Act
        var resolved = await sut.ResolveServiceItem(null, [], url);

        // Assert
        resolved.NonPodcastService.Should().Be(NonPodcastService.Netflix);
        resolved.Url.Should().Be(url);
        resolved.Title.Should().Be(_netflixMeta.Title);
        _mocker.GetMock<IVimeoMetaDataExtractor>()
            .Verify(x => x.GetMetaData(It.IsAny<Uri>()), Times.Never);
    }

    [Fact(DisplayName =
        "A URL whose host has no registered adapter cannot be resolved, " +
        "because extract routing only uses BBC, Internet Archive, Vimeo, Netflix, and Prime plugins.")]
    public async Task unknown_host_cannot_be_handled()
    {
        // Arrange
        var url = new Uri($"https://example.test/watch/{_fixture.CreateYouTubeId()}");
        var sut = _mocker.CreateInstance<StreamingServiceMetaDataHandler>();

        // Act
        var act = async () => await sut.ResolveServiceItem(null, [], url);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{url}*");
    }

    private NonPodcastServiceItemMetaData CreateMetaData() =>
        new(
            _fixture.CreateTitle(),
            _fixture.Create<string>(),
            _fixture.CreateDuration(),
            DomainTestFixture.UtcAtTime(-3, _fixture.CreateNonMidnightTimeOfDay()),
            _fixture.Create<Uri>(),
            false,
            _fixture.Create<string>(),
            _fixture.CreateTitle());

    private Uri BbcSoundsUrl() =>
        new($"https://www.bbc.co.uk/sounds/play/{_fixture.CreateYouTubeId()}");

    private Uri InternetArchiveUrl() =>
        new($"https://archive.org/details/{_fixture.CreateYouTubeId()}");
}
