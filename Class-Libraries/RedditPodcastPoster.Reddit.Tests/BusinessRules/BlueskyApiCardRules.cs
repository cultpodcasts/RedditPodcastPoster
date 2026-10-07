using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Bluesky.Client;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace RedditPodcastPoster.Reddit.Tests.BusinessRules;

public class BlueskyApiCardRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private Uri? _platform;
    private BlueskyPlatformCard? _platformCard;
    private BlueskyCardImage? _image;

    public BlueskyApiCardRules()
    {
        _mocker.GetMock<IBlueskyPlatformCardSource>()
            .Setup(source => source.TryGetAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Uri url, CancellationToken _) => url == _platform ? _platformCard : null);
        _mocker.GetMock<IBlueskyCardImageDownloader>()
            .Setup(downloader => downloader.Download(It.IsAny<IReadOnlyList<Uri>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _image);
    }

    [Fact(DisplayName =
        "When the public link is a short URL and the platform URL is YouTube, the API card still opens the short link, because the YouTube video API supplies the title and image and the short-url page is not fetched.")]
    public async Task short_url_with_youtube_platform_url_returns_an_api_card()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var platform = episode.Urls.YouTube!;
        var shortUrl = ArrangePlatform(platform);
        var sut = _mocker.CreateInstance<BlueskyFeedClient>();

        // Act
        var card = await sut.TryCreateApiCard(shortUrl, platform);

        // Assert
        card.Should().NotBeNull();
        card!.Link.Should().Be(shortUrl);
        card.Title.Should().Be(_platformCard!.Title);
        card.Description.Should().Be(_platformCard.Description);
        card.Image.Should().Be(_image);
    }

    [Fact(DisplayName =
        "When the public link is a short URL and the platform URL is Spotify, the API card still opens the short link, because the Spotify episode API supplies the title and image and the short-url page is not fetched.")]
    public async Task short_url_with_spotify_platform_url_returns_an_api_card()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var platform = episode.Urls.Spotify!;
        var shortUrl = ArrangePlatform(platform);
        var sut = _mocker.CreateInstance<BlueskyFeedClient>();

        // Act
        var card = await sut.TryCreateApiCard(shortUrl, platform);

        // Assert
        card.Should().NotBeNull();
        card!.Link.Should().Be(shortUrl);
        card.Title.Should().Be(_platformCard!.Title);
        card.Description.Should().Be(_platformCard.Description);
        card.Image.Should().Be(_image);
    }

    [Fact(DisplayName =
        "When the tallest card image is over the one megabyte thumb cap, the next smaller image is used and a card is still produced, because a maxres thumbnail must not drop the card.")]
    public async Task oversized_tallest_image_falls_through_to_the_next_url()
    {
        // Arrange
        var tall = new Uri($"https://cdn.example.test/{_fixture.CreateYouTubeId()}");
        var small = new Uri($"https://cdn.example.test/{_fixture.CreateYouTubeId()}");
        var oversized = new byte[BlueskyCardImageDownloader.MaxBytes + 1];
        var fitted = new byte[] { 0x01, 0x02, 0x03 };
        var handler = new ScriptedImageHandler();
        handler.Script(tall, oversized, "image/jpeg");
        handler.Script(small, fitted, "image/jpeg");
        var (sut, platform) = CreateFeedClient(handler, tall, small);

        // Act
        var card = await sut.TryCreateApiCard(platform, platform);

        // Assert
        card.Should().NotBeNull();
        card!.Image.Bytes.Should().Equal(fitted);
        card.Image.MimeType.Should().Be("image/jpeg");
        handler.Requested.Should().Equal(tall, small);
    }

    [Fact(DisplayName =
        "When a card image candidate is not an image, the next image URL is used, because a non-image body is not relabelled as jpeg.")]
    public async Task non_image_candidate_falls_through_to_the_next_url()
    {
        // Arrange
        var tall = new Uri($"https://cdn.example.test/{_fixture.CreateYouTubeId()}");
        var small = new Uri($"https://cdn.example.test/{_fixture.CreateYouTubeId()}");
        var html = new byte[] { 0x3C, 0x68 };
        var fitted = new byte[] { 0x04, 0x05 };
        var handler = new ScriptedImageHandler();
        handler.Script(tall, html, "text/html");
        handler.Script(small, fitted, "image/jpeg");
        var (sut, platform) = CreateFeedClient(handler, tall, small);

        // Act
        var card = await sut.TryCreateApiCard(platform, platform);

        // Assert
        card.Should().NotBeNull();
        card!.Image.Bytes.Should().Equal(fitted);
        card.Image.MimeType.Should().Be("image/jpeg");
        handler.Requested.Should().Equal(tall, small);
    }

    [Fact(DisplayName =
        "When every card image is over the thumb cap, no card is produced, because the lexicon thumb blob cannot accept it.")]
    public async Task every_image_over_the_cap_produces_no_card()
    {
        // Arrange
        var tall = new Uri($"https://cdn.example.test/{_fixture.CreateYouTubeId()}");
        var small = new Uri($"https://cdn.example.test/{_fixture.CreateYouTubeId()}");
        var oversized = new byte[BlueskyCardImageDownloader.MaxBytes + 1];
        var handler = new ScriptedImageHandler();
        handler.Script(tall, oversized, "image/jpeg");
        handler.Script(small, oversized, "image/jpeg");
        var (sut, platform) = CreateFeedClient(handler, tall, small);

        // Act
        var card = await sut.TryCreateApiCard(platform, platform);

        // Assert
        card.Should().BeNull();
        handler.Requested.Should().Equal(tall, small);
    }

    private Uri ArrangePlatform(Uri platform)
    {
        var title = _fixture.CreateTitle();
        var description = _fixture.Create<string>();
        var imageUrl = new Uri($"https://cdn.example.test/{_fixture.CreateYouTubeId()}");
        _platform = platform;
        _platformCard = new BlueskyPlatformCard(platform, title, description, [imageUrl]);
        _image = new BlueskyCardImage([0xFF, 0xD8], "image/jpeg");
        return new Uri($"https://s.cultpodcasts.com/{_fixture.CreateGuid():N}");
    }

    private (BlueskyFeedClient Client, Uri Platform) CreateFeedClient(
        ScriptedImageHandler handler,
        params Uri[] imageUrls)
    {
        var mocker = new AutoMocker();
        var platform = new Uri($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}");
        var title = _fixture.CreateTitle();
        var description = _fixture.Create<string>();
        mocker.Use<IBlueskyCardImageDownloader>(new BlueskyCardImageDownloader(handler));
        mocker.GetMock<IBlueskyPlatformCardSource>()
            .Setup(source => source.TryGetAsync(platform, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlueskyPlatformCard(platform, title, description, imageUrls));
        return (mocker.CreateInstance<BlueskyFeedClient>(), platform);
    }

    private sealed class ScriptedImageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (byte[] Body, string MediaType)> _responses = new(StringComparer.Ordinal);

        public List<Uri> Requested { get; } = [];

        internal void Script(Uri url, byte[] body, string mediaType)
        {
            _responses[url.AbsoluteUri] = (body, mediaType);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            Requested.Add(uri);
            var spec = _responses[uri.AbsoluteUri];
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(spec.Body)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(spec.MediaType);
            return Task.FromResult(response);
        }
    }
}
