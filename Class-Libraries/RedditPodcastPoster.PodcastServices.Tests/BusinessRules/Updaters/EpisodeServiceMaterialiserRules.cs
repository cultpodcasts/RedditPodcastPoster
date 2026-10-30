using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Apple.Providers;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Updaters;

public class EpisodeServiceMaterialiserRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Podcast _podcast;
    private readonly string _spotifyId;
    private readonly long _appleId;
    private readonly string _youTubeId;

    private Func<FullEpisode?> _spotifyResult = () => null;
    private Func<AppleEpisode?> _appleResult = () => null;
    private Func<Google.Apis.YouTube.v3.Data.Video?> _youTubeResult = () => null;
    private Uri? _youTubeImage;

    public EpisodeServiceMaterialiserRules()
    {
        _podcast = _fixture.CreatePodcast(p => p.AppleId = _fixture.CreateAppleId());
        _spotifyId = _fixture.CreateSpotifyId();
        _appleId = _fixture.CreateAppleId();
        _youTubeId = _fixture.CreateYouTubeId();
        _mocker.GetMock<ISpotifyEpisodeResolver>()
            .Setup(r => r.FindEpisode(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<Func<SimpleEpisode, bool>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new FindEpisodeResponse(_spotifyResult()));
        _mocker.GetMock<IApplePodcastService>()
            .Setup(s => s.GetEpisode(It.IsAny<long>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _appleResult());
        _mocker.GetMock<IYouTubeVideoService>()
            .Setup(s => s.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ReturnsAsync(() =>
                _youTubeResult() is { } video
                    ? (IList<Google.Apis.YouTube.v3.Data.Video>?)new List<Google.Apis.YouTube.v3.Data.Video> { video }
                    : null);
        _mocker.GetMock<IYouTubeThumbnailResolver>()
            .Setup(r => r.GetImageUrlAsync(It.IsAny<Google.Apis.YouTube.v3.Data.Video?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _youTubeImage);
    }

    private Episode EpisodeWithIdsOnly()
    {
        var episode = _fixture.CreateStoredEpisode(_podcast);
        episode.Services = null;
        episode.Ids = new EpisodeIds { Spotify = _spotifyId, Apple = _appleId, YouTube = _youTubeId };
        return episode;
    }

    private static FullEpisode SpotifyEpisode(string id, Uri url, Uri? image) =>
        new()
        {
            Id = id,
            ExternalUrls = new Dictionary<string, string> { ["spotify"] = url.ToString() },
            Images = image is null ? [] : [new Image { Height = 640, Width = 640, Url = image.ToString() }]
        };

    private AppleEpisode AppleItem(long id, Uri url, Uri? image) =>
        new(id, _fixture.CreateTitle(), DomainTestFixture.UtcDaysAgo(1), _fixture.CreateDuration(), url,
            _fixture.Create<string>(), false, image);

    private static Google.Apis.YouTube.v3.Data.Video YouTubeVideo(string id) => new() { Id = id };

    private EpisodeServiceMaterialiser Sut() => _mocker.CreateInstance<EpisodeServiceMaterialiser>();

    [Fact(DisplayName =
        "When an episode has Spotify, Apple and YouTube ids but no services, each platform's client url and image become Services entries, because catalogue playables keep Services only.")]
    public async Task ids_without_services_are_materialised_from_client_urls_and_images()
    {
        // Arrange
        var episode = EpisodeWithIdsOnly();
        var spotifyUrl = _fixture.Create<Uri>();
        var spotifyImage = _fixture.Create<Uri>();
        var appleUrl = _fixture.Create<Uri>();
        var appleImage = _fixture.Create<Uri>();
        _youTubeImage = _fixture.Create<Uri>();
        _spotifyResult = () => SpotifyEpisode(_spotifyId, spotifyUrl, spotifyImage);
        _appleResult = () => AppleItem(_appleId, appleUrl, appleImage);
        _youTubeResult = () => YouTubeVideo(_youTubeId);

        // Act
        await Sut().MaterialiseAsync(_podcast, episode);

        // Assert
        episode.Services.Should().NotBeNull();
        episode.Services![ServiceKeys.Spotify].Url.Should().Be(spotifyUrl);
        episode.Services[ServiceKeys.Spotify].Image.Should().Be(spotifyImage);
        episode.Services[ServiceKeys.Apple].Url.Should().Be(appleUrl);
        episode.Services[ServiceKeys.Apple].Image.Should().Be(appleImage);
        episode.Services[ServiceKeys.YouTube].Url.Should().Be(_fixture.DefaultYouTubeUrl(_youTubeId));
        episode.Services[ServiceKeys.YouTube].Image.Should().Be(_youTubeImage);
    }

    [Fact(DisplayName =
        "When services already has an image but no url, the url is filled from the client and the client image is preferred, because the image alone cannot play the episode.")]
    public async Task image_only_service_slot_gets_client_url_and_client_image()
    {
        // Arrange
        var episode = EpisodeWithIdsOnly();
        var storedImage = _fixture.Create<Uri>();
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Apple, null, storedImage);
        var appleUrl = _fixture.Create<Uri>();
        var clientImage = _fixture.Create<Uri>();
        _appleResult = () => AppleItem(_appleId, appleUrl, clientImage);

        // Act
        await Sut().MaterialiseAsync(_podcast, episode);

        // Assert
        episode.Services![ServiceKeys.Apple].Url.Should().Be(appleUrl);
        episode.Services[ServiceKeys.Apple].Image.Should().Be(clientImage);
    }

    [Fact(DisplayName =
        "When services already has an image but no url and the client returns no image, the stored image is kept and the url is still filled, because images must come over from the episode.")]
    public async Task stored_image_is_kept_when_client_has_no_image()
    {
        // Arrange
        var episode = EpisodeWithIdsOnly();
        var storedImage = _fixture.Create<Uri>();
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Spotify, null, storedImage);
        var spotifyUrl = _fixture.Create<Uri>();
        _spotifyResult = () => SpotifyEpisode(_spotifyId, spotifyUrl, null);

        // Act
        await Sut().MaterialiseAsync(_podcast, episode);

        // Assert
        episode.Services![ServiceKeys.Spotify].Url.Should().Be(spotifyUrl);
        episode.Services[ServiceKeys.Spotify].Image.Should().Be(storedImage);
    }

    [Fact(DisplayName =
        "When no client can supply a platform, the url is derived from the id for Spotify, Apple (with the podcast's Apple id) and YouTube, so every platform with an id still passes as a Services entry.")]
    public async Task clients_with_nothing_fall_back_to_id_derived_urls_for_all_platforms()
    {
        // Arrange
        var episode = EpisodeWithIdsOnly();

        // Act
        await Sut().MaterialiseAsync(_podcast, episode);

        // Assert
        episode.Services![ServiceKeys.Spotify].Url.Should().Be(_fixture.DefaultSpotifyUrl(_spotifyId));
        episode.Services[ServiceKeys.YouTube].Url.Should().Be(_fixture.DefaultYouTubeUrl(_youTubeId));
        episode.Services[ServiceKeys.Apple].Url.Should().Be(
            new Uri($"https://podcasts.apple.com/podcast/id{_podcast.AppleId}?i={_appleId}"));
    }

    [Fact(DisplayName =
        "When a client throws, materialising does not fail and the platform falls back to its id-derived url, because a lookup outage must not block a Move.")]
    public async Task client_failure_falls_back_to_id_derived_url()
    {
        // Arrange
        var episode = EpisodeWithIdsOnly();
        _spotifyResult = () => throw new InvalidOperationException(_fixture.Create<string>());
        _appleResult = () => throw new InvalidOperationException(_fixture.Create<string>());
        _youTubeResult = () => throw new InvalidOperationException(_fixture.Create<string>());

        // Act
        var act = () => Sut().MaterialiseAsync(_podcast, episode);

        // Assert
        await act.Should().NotThrowAsync();
        episode.Services![ServiceKeys.Spotify].Url.Should().Be(_fixture.DefaultSpotifyUrl(_spotifyId));
        episode.Services[ServiceKeys.Apple].Url.Should().NotBeNull();
        episode.Services[ServiceKeys.YouTube].Url.Should().Be(_fixture.DefaultYouTubeUrl(_youTubeId));
    }

    [Fact(DisplayName =
        "When a client returns an item with a different id than the episode's, that item is ignored, because a stale id must never attach another episode's link or image.")]
    public async Task client_item_with_a_different_id_is_ignored()
    {
        // Arrange
        var episode = EpisodeWithIdsOnly();
        var strayUrl = _fixture.Create<Uri>();
        var strayImage = _fixture.Create<Uri>();
        _spotifyResult = () => SpotifyEpisode(_fixture.CreateSpotifyId(), strayUrl, strayImage);
        _appleResult = () => AppleItem(_fixture.CreateAppleId(), strayUrl, strayImage);

        // Act
        await Sut().MaterialiseAsync(_podcast, episode);

        // Assert
        episode.Services![ServiceKeys.Spotify].Url.Should().Be(_fixture.DefaultSpotifyUrl(_spotifyId));
        episode.Services[ServiceKeys.Spotify].Image.Should().BeNull();
        episode.Services[ServiceKeys.Apple].Url.Should().NotBe(strayUrl);
        episode.Services[ServiceKeys.Apple].Image.Should().BeNull();
    }

    [Fact(DisplayName =
        "When an Apple id has no client link and the podcast has no Apple id, no Apple entry is invented, because the id-derived Apple url needs the show id.")]
    public async Task apple_fallback_needs_the_podcast_apple_id()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.AppleId = null);
        var episode = EpisodeWithIdsOnly();

        // Act
        await Sut().MaterialiseAsync(podcast, episode);

        // Assert
        episode.Services!.Should().NotContainKey(ServiceKeys.Apple);
        episode.Services.Should().ContainKey(ServiceKeys.Spotify);
        episode.Services.Should().ContainKey(ServiceKeys.YouTube);
    }

    [Fact(DisplayName =
        "When services already has a url for a platform, that platform is not looked up and its url and image are untouched, because only a missing url is materialised.")]
    public async Task platforms_that_already_have_a_url_are_not_looked_up()
    {
        // Arrange
        var episode = EpisodeWithIdsOnly();
        var spotifyUrl = _fixture.Create<Uri>();
        var spotifyImage = _fixture.Create<Uri>();
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Spotify, spotifyUrl, spotifyImage);
        _spotifyResult = () => SpotifyEpisode(_spotifyId, _fixture.Create<Uri>(), _fixture.Create<Uri>());

        // Act
        await Sut().MaterialiseAsync(_podcast, episode);

        // Assert
        episode.Services![ServiceKeys.Spotify].Url.Should().Be(spotifyUrl);
        episode.Services[ServiceKeys.Spotify].Image.Should().Be(spotifyImage);
        _mocker.GetMock<ISpotifyEpisodeResolver>().Verify(
            r => r.FindEpisode(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<Func<SimpleEpisode, bool>?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "Spotify is looked up by episode id only: the request carries the id and no show id, runs in the Index flow so no title search happens, and rejects every title-search candidate.")]
    public async Task spotify_lookup_is_by_id_only()
    {
        // Arrange
        var episode = EpisodeWithIdsOnly();
        FindSpotifyEpisodeRequest? captured = null;
        IndexingContext? capturedContext = null;
        Func<SimpleEpisode, bool>? capturedReducer = null;
        _mocker.GetMock<ISpotifyEpisodeResolver>()
            .Setup(r => r.FindEpisode(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<Func<SimpleEpisode, bool>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<FindSpotifyEpisodeRequest, IndexingContext, Func<SimpleEpisode, bool>?, CancellationToken>(
                (request, context, reducer, _) =>
                {
                    captured = request;
                    capturedContext = context;
                    capturedReducer = reducer;
                })
            .ReturnsAsync(new FindEpisodeResponse(null));

        // Act
        await Sut().MaterialiseAsync(_podcast, episode);

        // Assert
        captured.Should().NotBeNull();
        captured!.EpisodeSpotifyId.Should().Be(_spotifyId);
        captured.PodcastSpotifyId.Should().BeEmpty();
        capturedContext!.LookupFlow.Should().Be(EpisodeLookupFlow.Index);
        capturedReducer.Should().NotBeNull();
        capturedReducer!(new SimpleEpisode { Id = _spotifyId }).Should().BeFalse();
    }

    [Fact(DisplayName =
        "When the caller cancels, materialising stops with the cancellation instead of swallowing it as a client failure.")]
    public async Task cancellation_is_not_swallowed()
    {
        // Arrange
        var episode = EpisodeWithIdsOnly();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        var act = () => Sut().MaterialiseAsync(_podcast, episode, cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(DisplayName =
        "When given several episodes, each one with ids but no services is materialised, so a whole-podcast kind transfer keeps every platform on every playable.")]
    public async Task every_episode_in_a_collection_is_materialised()
    {
        // Arrange
        var first = EpisodeWithIdsOnly();
        var second = EpisodeWithIdsOnly();

        // Act
        await Sut().MaterialiseAsync(_podcast, [first, second]);

        // Assert
        first.Services.Should().ContainKeys(ServiceKeys.Spotify, ServiceKeys.YouTube, ServiceKeys.Apple);
        second.Services.Should().ContainKeys(ServiceKeys.Spotify, ServiceKeys.YouTube, ServiceKeys.Apple);
    }
}
