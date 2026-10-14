using System.Net;
using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;
using Xunit;
using static RedditPodcastPoster.Episodes.TestSupport.Fixtures.YouTubeThumbnailSpecimens;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules.Thumbnails;

/// <summary>
/// Thumbnail probe rules, replaying what i.ytimg.com served for a real episode on 2026-10-14:
/// maxresdefault and hq720 answered 404 with YouTube's 1097-byte 120x90 grey placeholder, while
/// sddefault (640x480, 60207 bytes), hqdefault (480x360, 39854 bytes) and mqdefault (320x180,
/// 20678 bytes) answered 200.
/// </summary>
public class YouTubeThumbnailProbeRules
{

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly YouTubeThumbnailHttpStub _http = new();
    private readonly string _youTubeId;

    public YouTubeThumbnailProbeRules()
    {
        _youTubeId = _fixture.CreateYouTubeId();
        _mocker.GetMock<IHttpClientFactory>()
            .Setup(factory => factory.CreateClient(YouTubeThumbnailResolver.HttpClientName))
            .Returns(() => new HttpClient(_http, disposeHandler: false));
    }

    [Fact(DisplayName =
        "When YouTube withdraws maxresdefault after release (404 with the 1097-byte 120x90 placeholder), " +
        "the resolver picks sddefault (640x480), because the largest real thumbnail wins and the placeholder is never stored.")]
    public async Task withdrawn_maxres_resolves_to_standard_tier()
    {
        // Arrange
        ServeRealCaseAfterWithdrawal();
        var sut = _mocker.CreateInstance<YouTubeThumbnailResolver>();

        // Act
        var image = await sut.GetImageUrlAsync(VideoWithApiThumbnails());

        // Assert
        image.Should().Be(Url(_youTubeId, StandardTier));
    }

    [Fact(DisplayName =
        "A thumbnail that answers a non-success status is unusable even when its body decodes as a large JPEG, " +
        "because a 404 means YouTube no longer serves that image.")]
    public async Task non_success_status_is_unusable_whatever_the_body()
    {
        // Arrange
        ServeRealCaseAfterWithdrawal();
        _http.Serve(Url(_youTubeId, MaxresTier), HttpStatusCode.NotFound, MaxresJpeg());
        var sut = _mocker.CreateInstance<YouTubeThumbnailResolver>();

        // Act
        var image = await sut.GetImageUrlAsync(VideoWithApiThumbnails());

        // Assert
        image.Should().Be(Url(_youTubeId, StandardTier));
    }

    [Fact(DisplayName =
        "A thumbnail whose download fails at the network layer is skipped and the next largest real thumbnail wins, " +
        "because one unreachable tier must not leave the episode without an image.")]
    public async Task network_failure_falls_through_to_next_candidate()
    {
        // Arrange
        ServeRealCaseAfterWithdrawal();
        _http.FailNetwork(Url(_youTubeId, StandardTier));
        var sut = _mocker.CreateInstance<YouTubeThumbnailResolver>();

        // Act
        var image = await sut.GetImageUrlAsync(VideoWithApiThumbnails());

        // Assert
        image.Should().Be(Url(_youTubeId, HighTier));
    }

    [Fact(DisplayName =
        "A stored maxresdefault image that now answers 404 with the 120x90 placeholder is no longer usable, " +
        "so the delayed re-check knows to replace it.")]
    public async Task stored_withdrawn_maxres_is_not_usable()
    {
        // Arrange
        ServeRealCaseAfterWithdrawal();
        var sut = _mocker.CreateInstance<YouTubeThumbnailResolver>();

        // Act
        var usable = await sut.IsStoredImageUsableAsync(Url(_youTubeId, MaxresTier));

        // Assert
        usable.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A stored maxresdefault image that still answers 200 with a 1280x720 JPEG stays usable, " +
        "so the delayed re-check leaves it alone.")]
    public async Task stored_live_maxres_is_usable()
    {
        // Arrange
        _http.Serve(Url(_youTubeId, MaxresTier), HttpStatusCode.OK, MaxresJpeg());
        var sut = _mocker.CreateInstance<YouTubeThumbnailResolver>();

        // Act
        var usable = await sut.IsStoredImageUsableAsync(Url(_youTubeId, MaxresTier));

        // Assert
        usable.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A stored image on the default tier is usable at 120x90, because default.jpg is that size by design.")]
    public async Task stored_default_tier_is_usable_at_placeholder_height()
    {
        // Arrange
        _http.Serve(Url(_youTubeId, DefaultTier), HttpStatusCode.OK, DefaultJpeg());
        var sut = _mocker.CreateInstance<YouTubeThumbnailResolver>();

        // Act
        var usable = await sut.IsStoredImageUsableAsync(Url(_youTubeId, DefaultTier));

        // Assert
        usable.Should().BeTrue();
    }

    private void ServeRealCaseAfterWithdrawal()
    {
        var placeholder = WithdrawnPlaceholderJpeg();
        _http.Serve(Url(_youTubeId, MaxresTier), HttpStatusCode.NotFound, placeholder)
            .Serve(Url(_youTubeId, Hq720Tier), HttpStatusCode.NotFound, placeholder)
            .Serve(Url(_youTubeId, StandardTier), HttpStatusCode.OK,
                StandardJpeg())
            .Serve(Url(_youTubeId, HighTier), HttpStatusCode.OK, HighJpeg())
            .Serve(Url(_youTubeId, MediumTier), HttpStatusCode.OK, MediumJpeg());
    }

    private Google.Apis.YouTube.v3.Data.Video VideoWithApiThumbnails() => new()
    {
        Id = _youTubeId,
        Snippet = new VideoSnippet
        {
            Thumbnails = new ThumbnailDetails
            {
                Maxres = Thumbnail(MaxresTier, MaxresHeight),
                Standard = Thumbnail(StandardTier, StandardHeight),
                High = Thumbnail(HighTier, HighHeight),
                Medium = Thumbnail(MediumTier, MediumHeight),
                Default__ = Thumbnail(DefaultTier, DefaultHeight)
            }
        }
    };

    private Thumbnail Thumbnail(string tier, int height) =>
        new() { Url = Url(_youTubeId, tier).AbsoluteUri, Height = height };
}
