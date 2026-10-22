using System.Net;
using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;
using Xunit;
using static RedditPodcastPoster.Episodes.TestSupport.Fixtures.YouTubeThumbnailSpecimens;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules.Thumbnails;

/// <summary>
/// Probing thumbnails runs once per discovered video inside the indexer's per-podcast budget, so a probe must be cheap
/// and a slow image host must cost one candidate, not the podcast.
/// </summary>
public class YouTubeThumbnailProbeCostRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly YouTubeThumbnailHttpStub _http = new();
    private readonly RecordingLogger<YouTubeThumbnailResolver> _logger = new();
    private readonly string _youTubeId;
    private TimeSpan _clientTimeout = TimeSpan.FromSeconds(30);

    public YouTubeThumbnailProbeCostRules()
    {
        _youTubeId = _fixture.CreateYouTubeId();
        _mocker.Use<ILogger<YouTubeThumbnailResolver>>(_logger);
        _mocker.GetMock<IHttpClientFactory>()
            .Setup(factory => factory.CreateClient(YouTubeThumbnailResolver.HttpClientName))
            .Returns(() => new HttpClient(_http, disposeHandler: false) { Timeout = _clientTimeout });
        _http.Serve(Url(_youTubeId, MaxresTier), HttpStatusCode.OK, MaxresJpeg())
            .Serve(Url(_youTubeId, StandardTier), HttpStatusCode.OK, StandardJpeg())
            .Serve(Url(_youTubeId, HighTier), HttpStatusCode.OK, HighJpeg());
    }

    [Fact(DisplayName =
        "A thumbnail probe asks for only the leading bytes that hold the JPEG dimensions (an HTTP Range request), " +
        "because downloading every candidate image in full is wasted time inside the podcast's budget.")]
    public async Task probe_requests_only_the_jpeg_header_range()
    {
        // Arrange
        var sut = _mocker.CreateInstance<YouTubeThumbnailResolver>();

        // Act
        var image = await sut.GetImageUrlAsync(Video(Thumbnail(MaxresTier, MaxresHeight)));

        // Assert
        image.Should().Be(Url(_youTubeId, MaxresTier));
        var range = _http.RequestedRanges.Should().ContainSingle().Subject;
        range.Should().NotBeNull();
        range!.Ranges.Should().ContainSingle().Which.Should().Match<System.Net.Http.Headers.RangeItemHeaderValue>(
            item => item.From == 0 && item.To == YouTubeThumbnailResolver.ProbeMaxBytes - 1);
    }

    [Fact(DisplayName =
        "A non-default tier the YouTube API already reports at or below the 90px placeholder height is skipped without " +
        "a request, because the API dimensions already rule it out.")]
    public async Task api_reported_placeholder_height_is_not_probed()
    {
        // Arrange
        var sut = _mocker.CreateInstance<YouTubeThumbnailResolver>();
        var tooShort = Thumbnail(MaxresTier, DefaultHeight);

        // Act
        var image = await sut.GetImageUrlAsync(Video(tooShort, Thumbnail(StandardTier, StandardHeight)));

        // Assert
        image.Should().Be(Url(_youTubeId, StandardTier));
        _http.Requested.Should().NotContain(Url(_youTubeId, MaxresTier));
    }

    [Fact(DisplayName =
        "When a thumbnail tier does not answer within the probe timeout, the API-listed tier is kept and a Warning names " +
        "the video, because a slow image host says nothing about the tier and storing a lower tier would downgrade the " +
        "image for good (the recent-thumbnail re-check verifies high-res tiers later).")]
    public async Task probe_timeout_keeps_the_api_candidate_and_warns()
    {
        // Arrange
        _clientTimeout = TimeSpan.FromMilliseconds(50);
        _http.Hang(Url(_youTubeId, MaxresTier));
        var sut = _mocker.CreateInstance<YouTubeThumbnailResolver>();

        // Act
        var image = await sut.GetImageUrlAsync(Video(
            Thumbnail(MaxresTier, MaxresHeight),
            Thumbnail(StandardTier, StandardHeight)));

        // Assert
        image.Should().Be(Url(_youTubeId, MaxresTier));
        _http.Requested.Should().NotContain(Url(_youTubeId, StandardTier));
        _logger.Entries.Should().ContainSingle(entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.StartsWith(YouTubeThumbnailResolver.ProbeTimeoutMessagePrefix) &&
            entry.Message.Contains($"video-id='{_youTubeId}'"));
    }

    [Fact(DisplayName =
        "When a stored image's probe hits the probe client's own timeout, the check throws instead of answering " +
        "'withdrawn', because the re-check must never re-resolve an image on a slow CDN.")]
    public async Task stored_image_probe_timeout_is_not_reported_as_withdrawn()
    {
        // Arrange
        _clientTimeout = TimeSpan.FromMilliseconds(50);
        _http.Hang(Url(_youTubeId, MaxresTier));
        var sut = _mocker.CreateInstance<YouTubeThumbnailResolver>();

        // Act
        var act = () => sut.IsStoredImageUsableAsync(Url(_youTubeId, MaxresTier));

        // Assert
        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact(DisplayName =
        "When the caller cancels (the podcast's budget elapsed), the probe stops and cancellation propagates, " +
        "because the indexer must move on rather than keep probing remaining tiers.")]
    public async Task caller_cancellation_propagates()
    {
        // Arrange
        _http.Hang(Url(_youTubeId, MaxresTier));
        var sut = _mocker.CreateInstance<YouTubeThumbnailResolver>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        // Act
        var act = () => sut.GetImageUrlAsync(
            Video(Thumbnail(MaxresTier, MaxresHeight), Thumbnail(StandardTier, StandardHeight)),
            cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        _http.Requested.Should().NotContain(Url(_youTubeId, StandardTier));
    }

    private Google.Apis.YouTube.v3.Data.Video Video(params Thumbnail[] byTier)
    {
        var details = new ThumbnailDetails();
        foreach (var thumbnail in byTier)
        {
            if (thumbnail.Url.Contains(MaxresTier))
            {
                details.Maxres = thumbnail;
            }
            else if (thumbnail.Url.Contains(StandardTier))
            {
                details.Standard = thumbnail;
            }
            else
            {
                details.High = thumbnail;
            }
        }

        return new Google.Apis.YouTube.v3.Data.Video
        {
            Id = _youTubeId,
            Snippet = new VideoSnippet { Thumbnails = details }
        };
    }

    private Thumbnail Thumbnail(string tier, int height) =>
        new() { Url = Url(_youTubeId, tier).AbsoluteUri, Height = height };
}
