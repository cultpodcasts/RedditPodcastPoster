using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.BBC.Extractors;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Services;
using RedditPodcastPoster.UrlSubmission.Tests.Support;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class UrlMembershipLookupRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly InMemoryEpisodeRepository _episodes = new();
    private readonly InMemoryPodcastRepository _podcasts = new();
    private Func<Uri, Task<NonPodcastServiceItemMetaData>>? _vimeoExtract;

    public UrlMembershipLookupRules()
    {
        _mocker.Use<IEpisodeRepository>(_episodes);
        _mocker.Use<IPodcastRepository>(_podcasts);
        _mocker.Use<INonPodcastServiceAdapterResolver>(NonPodcastSubmitAdapterResolverSupport.Create(
            _mocker.GetMock<IBBCPageMetaDataExtractor>().Object,
            url => _vimeoExtract != null
                ? _vimeoExtract(url)
                : throw new InvalidOperationException("Extract is not used in submit routing tests.")));
    }

    [Fact(DisplayName =
        "When a Spotify episode URL is already stored on one series, URL membership lookup returns that podcast id and name without writing episodes.")]
    public async Task known_spotify_url_returns_unique_series()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var url = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify)!;
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeTrue();
        result.PodcastId.Should().Be(podcast.Id);
        result.PodcastName.Should().Be(podcast.Name);
        result.Kind.Should().Be(UrlMembershipLookupKinds.PodcastService);
        result.Ambiguous.Should().BeFalse();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a Spotify share URL with a tracking query is pasted, URL membership lookup matches nested ids.spotify (and the cleaned catalog URL) on the existing series and does not write episodes.")]
    public async Task known_spotify_share_query_matches_nested_id()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var storedUrl = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify)!;
        var url = new Uri(storedUrl.AbsoluteUri + "?si=" + _fixture.CreateSpotifyId());
        url.Should().NotBe(storedUrl);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeTrue();
        result.PodcastId.Should().Be(podcast.Id);
        result.Kind.Should().Be(UrlMembershipLookupKinds.PodcastService);
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a youtu.be URL is pasted and the series stores watch?v= plus ids.youtube, URL membership lookup returns that podcast without writing episodes.")]
    public async Task known_youtube_short_url_matches_nested_id()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var youTubeId = EpisodeServicePresence.YouTubeEpisodeId(episode)!;
        var storedUrl = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.YouTube)!;
        var url = new Uri($"https://youtu.be/{youTubeId}");
        url.Should().NotBe(storedUrl);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeTrue();
        result.PodcastId.Should().Be(podcast.Id);
        result.Kind.Should().Be(UrlMembershipLookupKinds.PodcastService);
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When an Apple episode URL uses a different locale path than the stored /us/ catalog URL, URL membership lookup matches ids.apple and does not write episodes.")]
    public async Task known_apple_locale_path_matches_nested_id()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var appleEpisodeId = _fixture.CreateAppleId();
        var applePodcastId = _fixture.CreateAppleId();
        var storedUrl = new Uri($"https://podcasts.apple.com/us/podcast/episode/id{applePodcastId}?i={appleEpisodeId}");
        var url = new Uri($"https://podcasts.apple.com/gb/podcast/episode/id{applePodcastId}?i={appleEpisodeId}");
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            EpisodeServicePresence.SetAppleIdentity(e, appleEpisodeId);
            EpisodeServicePresence.Upsert(e, ServiceKeys.Apple, storedUrl, null);
        });
        url.Should().NotBe(storedUrl);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeTrue();
        result.PodcastId.Should().Be(podcast.Id);
        result.Kind.Should().Be(UrlMembershipLookupKinds.PodcastService);
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a Spotify episode URL is not stored, URL membership lookup returns unknown podcast-service so submit can still create from platform metadata.")]
    public async Task unknown_spotify_url_returns_podcast_service()
    {
        // Arrange
        var url = _fixture.DefaultSpotifyUrl(_fixture.CreateSpotifyId());
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(new UrlMembershipLookupResult(
            false,
            UrlMembershipLookupKinds.PodcastService));
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a streaming URL is already stored on one series, URL membership lookup returns that podcast from URL membership only and does not scrape metadata.")]
    public async Task known_sounds_url_returns_unique_series()
    {
        // Arrange
        var url = BbcSoundsUrl();
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast, e => SeedBbcSoundsLookup(e, url));
        _mocker.GetMock<IBBCPageMetaDataExtractor>()
            .Setup(e => e.GetMetaData(It.IsAny<Uri>()))
            .ReturnsAsync(new NonPodcastServiceItemMetaData(
                Title: _fixture.CreateTitle(),
                Description: _fixture.Create<string>(),
                ShowName: _fixture.CreateTitle()));
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeTrue();
        result.PodcastId.Should().Be(podcast.Id);
        result.PodcastName.Should().Be(podcast.Name);
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        _episodes.SavedEpisodes.Should().BeEmpty();
        _mocker.GetMock<IBBCPageMetaDataExtractor>().Verify(e => e.GetMetaData(It.IsAny<Uri>()), Times.Never);
    }

    [Fact(DisplayName =
        "When a streaming URL is not stored, URL membership lookup returns unknown streaming without a series name unless extract supplies ShowName.")]
    public async Task unknown_sounds_url_returns_streaming()
    {
        // Arrange
        var url = BbcSoundsUrl();
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(new UrlMembershipLookupResult(
            false,
            UrlMembershipLookupKinds.Streaming));
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a streaming URL is not stored, URL membership lookup extracts the adapter series/show name " +
        "so drop can persist podcastName; known stays false because membership is URL-only.")]
    public async Task unknown_streaming_extracts_show_name()
    {
        // Arrange
        var url = BbcSoundsUrl();
        var showName = _fixture.CreateTitle();
        _mocker.GetMock<IBBCPageMetaDataExtractor>()
            .Setup(e => e.GetMetaData(url))
            .ReturnsAsync(new NonPodcastServiceItemMetaData(
                Title: _fixture.CreateTitle(),
                Description: _fixture.Create<string>(),
                ShowName: showName));
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeFalse();
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        result.PodcastName.Should().Be(showName);
        result.PodcastId.Should().BeNull();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When unknown streaming metadata has a publisher but no series/show name, URL membership lookup leaves PodcastName null " +
        "because publisher is a platform brand, not a series, and drop must not name-attach to it.")]
    public async Task unknown_streaming_publisher_only_leaves_podcast_name_null()
    {
        // Arrange
        var url = BbcSoundsUrl();
        _mocker.GetMock<IBBCPageMetaDataExtractor>()
            .Setup(e => e.GetMetaData(url))
            .ReturnsAsync(new NonPodcastServiceItemMetaData(
                Title: _fixture.CreateTitle(),
                Description: _fixture.Create<string>(),
                Publisher: _fixture.Create<string>(),
                ShowName: null));
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeFalse();
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        result.PodcastName.Should().BeNull();
        result.PodcastId.Should().BeNull();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When streaming metadata extract throws, URL membership lookup still returns unknown streaming with a null podcastName " +
        "so GET lookup does not fail the request.")]
    public async Task unknown_streaming_extract_failure_leaves_podcast_name_null()
    {
        // Arrange
        var url = BbcSoundsUrl();
        _mocker.GetMock<IBBCPageMetaDataExtractor>()
            .Setup(e => e.GetMetaData(url))
            .ThrowsAsync(new InvalidOperationException(_fixture.Create<string>()));
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeFalse();
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        result.PodcastName.Should().BeNull();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When an unknown Vimeo URL has no series/show name, URL membership lookup uses the author as podcastName " +
        "because Vimeo publisher is the uploader, not a platform brand.")]
    public async Task unknown_vimeo_publisher_is_series_name()
    {
        // Arrange
        var url = new Uri($"https://vimeo.com/{_fixture.CreateAppleId()}");
        var author = _fixture.Create<string>();
        _vimeoExtract = _ => Task.FromResult(new NonPodcastServiceItemMetaData(
            Title: _fixture.CreateTitle(),
            Description: _fixture.Create<string>(),
            Publisher: author,
            ShowName: null));
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeFalse();
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        result.PodcastName.Should().Be(author);
        result.PodcastId.Should().BeNull();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the same stored URL belongs to more than one podcast, URL membership lookup returns known false with ambiguous true and the podcast ids, because the UI must still offer Series.")]
    public async Task ambiguous_stored_url_returns_podcast_ids()
    {
        // Arrange
        var url = BbcSoundsUrl();
        var first = _fixture.CreatePodcast();
        var second = _fixture.CreatePodcast();
        _podcasts.Seed(first, second);
        _episodes.Seed(
            _fixture.CreateStoredEpisode(first, e => SeedBbcSoundsLookup(e, url)),
            _fixture.CreateStoredEpisode(second, e => SeedBbcSoundsLookup(e, url)));
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeFalse();
        result.Ambiguous.Should().BeTrue();
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        result.PodcastIds.Should().BeEquivalentTo([first.Id, second.Id]);
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the host is not a submittable podcast-service or streaming URL, URL membership lookup returns unrecognised without querying writes.")]
    public async Task unrecognised_host_returns_unrecognised_kind()
    {
        // Arrange
        var url = new Uri($"https://example.com/{_fixture.CreateGuid():N}");
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(new UrlMembershipLookupResult(
            false,
            UrlMembershipLookupKinds.Unrecognised));
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the host is a lookalike of a podcast-service domain, URL membership lookup returns unrecognised and does not run a catalog membership query.")]
    public async Task lookalike_spotify_host_returns_unrecognised()
    {
        // Arrange
        var url = new Uri($"https://open.spotify.com.example.test/episode/{_fixture.CreateSpotifyId()}");
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(new UrlMembershipLookupResult(
            false,
            UrlMembershipLookupKinds.Unrecognised));
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    private Uri BbcSoundsUrl() =>
        new($"https://www.bbc.co.uk/sounds/play/{_fixture.CreateYouTubeId()}");

    private static void SeedBbcSoundsLookup(Episode episode, Uri soundsUrl)
    {
        episode.Services = new Dictionary<string, EpisodeServiceLink>(StringComparer.Ordinal)
        {
            [ServiceKeys.BbcIplayer] = new(),
            [ServiceKeys.BbcSounds] = new() { Url = soundsUrl }
        };
    }
}
