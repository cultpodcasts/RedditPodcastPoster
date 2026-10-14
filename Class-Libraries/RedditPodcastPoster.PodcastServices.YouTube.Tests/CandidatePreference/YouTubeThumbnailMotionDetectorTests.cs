using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.PodcastServices.YouTube.CandidatePreference;
using SkiaSharp;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.CandidatePreference;

public class YouTubeThumbnailMotionDetectorTests
{
    private const string VideoId = "S03n89L5YLA";

    /// <summary>
    /// October 2026 calibration: still-image uploads, then the gap, then the lowest real video.
    /// SkiaSharp remeasurement left 3.10 / 6.93 / threshold 5.0 in place.
    /// </summary>
    private const double StillImageDifferenceFloor = 0.07;

    private const double StillImageDifferenceCeiling = 3.10;
    private const double LowestRealVideoDifference = 6.93;

    private readonly AutoMocker _mocker = new();
    private readonly StubHandler _handler = new();
    private readonly YouTubeCandidatePreferenceOptions _options = new() { ThumbnailFetchTimeoutSeconds = 1 };

    public YouTubeThumbnailMotionDetectorTests()
    {
        _mocker.Use(Options.Create(_options));
        _mocker.Use<ILogger<YouTubeThumbnailMotionDetector>>(NullLogger<YouTubeThumbnailMotionDetector>.Instance);
        _mocker.GetMock<IHttpClientFactory>()
            .Setup(x => x.CreateClient(YouTubeThumbnailMotionDetector.HttpClientName))
            .Returns(() =>
            {
                var client = new HttpClient(_handler, disposeHandler: false);
                YouTubeThumbnailMotionDetector.ApplyClientLimits(client, _options);
                return client;
            });
    }

    private YouTubeThumbnailMotionDetector Sut() => _mocker.CreateInstance<YouTubeThumbnailMotionDetector>();

    [Fact(DisplayName = "Near-identical auto thumbnails (still-image upload) are Static.")]
    public async Task identical_frames_are_static()
    {
        // Arrange
        Serve(Jpeg(seed: 1), Jpeg(seed: 1), Jpeg(seed: 1));
        var sut = Sut();

        // Act
        var motion = await sut.DetectAsync(VideoId);

        // Assert
        motion.Should().Be(ThumbnailMotion.Static);
        _handler.Requested.Should().BeEquivalentTo(
        [
            "https://i.ytimg.com/vi/S03n89L5YLA/1.jpg",
            "https://i.ytimg.com/vi/S03n89L5YLA/2.jpg",
            "https://i.ytimg.com/vi/S03n89L5YLA/3.jpg"
        ]);
    }

    [Fact(DisplayName = "Differing auto thumbnails (real video) are Moving.")]
    public async Task differing_frames_are_moving()
    {
        // Arrange
        Serve(Jpeg(seed: 1), Jpeg(seed: 2), Jpeg(seed: 3));
        var sut = Sut();

        // Act
        var motion = await sut.DetectAsync(VideoId);

        // Assert
        motion.Should().Be(ThumbnailMotion.Moving);
    }

    [Fact(DisplayName =
        "A frame difference in the calibrated still-image band (0.07-3.10) is Static at the default threshold of 5.0, " +
        "which sits below the lowest real video (6.93).")]
    public async Task still_image_band_is_static_at_default_threshold()
    {
        // Arrange
        var threshold = new YouTubeCandidatePreferenceOptions().StaticImageThreshold;
        var basis = BandedFrame();
        var offset = OffsetSampledRed(basis, delta: 1);
        var difference = ThumbnailFrame.MeanAbsoluteDifference(basis, offset);
        Serve(Png(basis), Png(offset), Png(basis));
        var sut = Sut();

        // Act
        var motion = await sut.DetectAsync(VideoId);

        // Assert
        threshold.Should().Be(5.0);
        threshold.Should().BeGreaterThan(StillImageDifferenceCeiling);
        threshold.Should().BeLessThan(LowestRealVideoDifference);
        difference.Should().BeInRange(StillImageDifferenceFloor, StillImageDifferenceCeiling);
        difference.Should().BeLessThan(threshold);
        basis.PixelStandardDeviation().Should().BeGreaterThan(_options.PlaceholderMaxPixelStdDev);
        motion.Should().Be(ThumbnailMotion.Static);
    }

    [Fact(DisplayName =
        "A frame difference at or above the lowest calibrated real video (6.93) is Moving at the default threshold of 5.0.")]
    public async Task real_video_band_is_moving_at_default_threshold()
    {
        // Arrange
        var threshold = new YouTubeCandidatePreferenceOptions().StaticImageThreshold;
        var basis = BandedFrame();
        var offset = OffsetSampledRed(basis, delta: 21);
        var difference = ThumbnailFrame.MeanAbsoluteDifference(basis, offset);
        Serve(Png(basis), Png(offset), Png(basis));
        var sut = Sut();

        // Act
        var motion = await sut.DetectAsync(VideoId);

        // Assert
        threshold.Should().Be(5.0);
        threshold.Should().BeGreaterThan(StillImageDifferenceCeiling);
        threshold.Should().BeLessThan(LowestRealVideoDifference);
        difference.Should().BeInRange(LowestRealVideoDifference, LowestRealVideoDifference + 4);
        difference.Should().BeGreaterThan(threshold);
        motion.Should().Be(ThumbnailMotion.Moving);
    }

    [Fact(DisplayName = "A missing auto thumbnail gives Unknown (fail-open).")]
    public async Task missing_thumbnail_is_unknown()
    {
        // Arrange
        _handler.Respond = url => url.EndsWith("2.jpg")
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : Ok(Jpeg(seed: 1));
        var sut = Sut();

        // Act
        var motion = await sut.DetectAsync(VideoId);

        // Assert
        motion.Should().Be(ThumbnailMotion.Unknown);
    }

    [Fact(DisplayName = "Uniform grey placeholder thumbnails give Unknown, not Static (fail-open).")]
    public async Task placeholder_is_unknown()
    {
        // Arrange
        var grey = Jpeg(seed: null);
        Serve(grey, grey, grey);
        var sut = Sut();

        // Act
        var motion = await sut.DetectAsync(VideoId);

        // Assert
        motion.Should().Be(ThumbnailMotion.Unknown);
    }

    [Fact(DisplayName = "Undecodable thumbnail bytes give Unknown.")]
    public async Task undecodable_is_unknown()
    {
        // Arrange
        Serve([1, 2, 3], [1, 2, 3], [1, 2, 3]);
        var sut = Sut();

        // Act
        var motion = await sut.DetectAsync(VideoId);

        // Assert
        motion.Should().Be(ThumbnailMotion.Unknown);
    }

    [Fact(DisplayName = "A network failure gives Unknown.")]
    public async Task network_failure_is_unknown()
    {
        // Arrange
        _handler.Respond = _ => throw new HttpRequestException("down");
        var sut = Sut();

        // Act
        var motion = await sut.DetectAsync(VideoId);

        // Assert
        motion.Should().Be(ThumbnailMotion.Unknown);
    }

    [Fact(DisplayName = "An unexpected failure while reading a thumbnail gives Unknown (fail-open).")]
    public async Task unexpected_failure_is_unknown()
    {
        // Arrange
        _handler.Respond = _ => throw new InvalidOperationException("boom");
        var sut = Sut();

        // Act
        var motion = await sut.DetectAsync(VideoId);

        // Assert
        motion.Should().Be(ThumbnailMotion.Unknown);
    }

    [Fact(DisplayName = "A fetch that exceeds ThumbnailFetchTimeoutSeconds gives Unknown.")]
    public async Task timeout_is_unknown()
    {
        // Arrange
        _handler.Delay = TimeSpan.FromSeconds(10);
        Serve(Jpeg(seed: 1), Jpeg(seed: 1), Jpeg(seed: 1));
        var sut = Sut();

        // Act
        var motion = await sut.DetectAsync(VideoId);

        // Assert
        motion.Should().Be(ThumbnailMotion.Unknown);
    }

    [Fact(DisplayName = "Caller cancellation is propagated and is not reported as Unknown.")]
    public async Task caller_cancellation_is_propagated()
    {
        // Arrange
        _handler.Delay = TimeSpan.FromSeconds(10);
        Serve(Jpeg(seed: 1), Jpeg(seed: 1), Jpeg(seed: 1));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var sut = Sut();

        // Act
        var act = () => sut.DetectAsync(VideoId, cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(DisplayName = "Thumbnails of different sizes give Unknown.")]
    public async Task mismatched_sizes_are_unknown()
    {
        // Arrange
        Serve(Jpeg(seed: 1), Jpeg(seed: 1, width: 160), Jpeg(seed: 1));
        var sut = Sut();

        // Act
        var motion = await sut.DetectAsync(VideoId);

        // Assert
        motion.Should().Be(ThumbnailMotion.Unknown);
    }

    [Fact(DisplayName = "The static threshold comes from options.")]
    public async Task threshold_is_configurable()
    {
        // Arrange
        _options.StaticImageThreshold = 255;
        Serve(Jpeg(seed: 1), Jpeg(seed: 2), Jpeg(seed: 3));
        var sut = Sut();

        // Act
        var motion = await sut.DetectAsync(VideoId);

        // Assert
        motion.Should().Be(ThumbnailMotion.Static);
    }

    [Fact(DisplayName =
        "A thumbnail body over MaxThumbnailBytes is Unknown and is not classified Static, even when the bytes are a still image.")]
    public async Task body_over_byte_ceiling_is_unknown()
    {
        // Arrange
        var still = Jpeg(seed: 1);
        _options.MaxThumbnailBytes = still.Length - 1;
        Serve(still, still, still);
        var sut = Sut();

        // Act
        var motion = await sut.DetectAsync(VideoId);

        // Assert
        motion.Should().Be(ThumbnailMotion.Unknown);
    }

    [Fact(DisplayName =
        "The thumbnail client buffers at most MaxThumbnailBytes and follows only MaxThumbnailRedirects redirects.")]
    public void client_limits_come_from_options()
    {
        // Arrange
        var settings = new YouTubeCandidatePreferenceOptions();
        using var client = new HttpClient();

        // Act
        YouTubeThumbnailMotionDetector.ApplyClientLimits(client, settings);
        using var handler = YouTubeThumbnailMotionDetector.CreateThumbnailHandler(settings);

        // Assert
        client.MaxResponseContentBufferSize.Should().Be(settings.MaxThumbnailBytes);
        client.MaxResponseContentBufferSize.Should().Be(YouTubeCandidatePreferenceOptions.DefaultMaxThumbnailBytes);
        handler.AllowAutoRedirect.Should().BeTrue();
        handler.MaxAutomaticRedirections.Should().Be(settings.MaxThumbnailRedirects);
        handler.MaxAutomaticRedirections.Should().BeInRange(
            YouTubeCandidatePreferenceOptions.MinimumMaxThumbnailRedirects,
            YouTubeCandidatePreferenceOptions.MaximumMaxThumbnailRedirects);
    }

    private void Serve(byte[] one, byte[] two, byte[] three) =>
        _handler.Respond = url => Ok(url.EndsWith("1.jpg") ? one : url.EndsWith("2.jpg") ? two : three);

    private static HttpResponseMessage Ok(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };

    /// <summary>120x90 JPEG: a seeded random block pattern, or uniform grey when seed is null.</summary>
    private static byte[] Jpeg(int? seed, int width = 120)
    {
        const int height = 90;
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var bitmap = new SKBitmap(info);
        bitmap.Erase(new SKColor(128, 128, 128));
        if (seed.HasValue)
        {
            var random = new Random(seed.Value);
            for (var by = 0; by < height; by += 10)
            for (var bx = 0; bx < width; bx += 10)
            {
                var colour = new SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
                for (var y = by; y < by + 10 && y < height; y++)
                for (var x = bx; x < bx + 10 && x < width; x++)
                {
                    bitmap.SetPixel(x, y, colour);
                }
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
        return data.ToArray();
    }

    /// <summary>120x90 RGB with variation, and red values low enough that a small sampled offset cannot wrap.</summary>
    private static ThumbnailFrame BandedFrame()
    {
        const int width = 120;
        const int height = 90;
        var rgb = new byte[width * height * 3];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var i = (y * width + x) * 3;
            rgb[i] = (byte)(40 + (x % 20) * 8);
            rgb[i + 1] = (byte)(30 + (y % 15) * 10);
            rgb[i + 2] = 90;
        }

        return new ThumbnailFrame(width, height, rgb);
    }

    private static ThumbnailFrame OffsetSampledRed(ThumbnailFrame source, byte delta)
    {
        var rgb = (byte[])source.Rgb.Clone();
        for (var y = 0; y < source.Height; y += ThumbnailFrame.SampleStep)
        for (var x = 0; x < source.Width; x += ThumbnailFrame.SampleStep)
        {
            var i = (y * source.Width + x) * 3;
            rgb[i] = (byte)(rgb[i] + delta);
        }

        return new ThumbnailFrame(source.Width, source.Height, rgb);
    }

    private static byte[] Png(ThumbnailFrame frame)
    {
        var info = new SKImageInfo(frame.Width, frame.Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var bitmap = new SKBitmap(info);
        for (var y = 0; y < frame.Height; y++)
        for (var x = 0; x < frame.Width; x++)
        {
            var i = (y * frame.Width + x) * 3;
            bitmap.SetPixel(x, y, new SKColor(frame.Rgb[i], frame.Rgb[i + 1], frame.Rgb[i + 2]));
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public Func<string, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.NotFound);
        public TimeSpan Delay { get; set; } = TimeSpan.Zero;
        public List<string> Requested { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Requested)
            {
                Requested.Add(request.RequestUri!.ToString());
            }

            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }

            return Respond(request.RequestUri!.ToString());
        }
    }
}
