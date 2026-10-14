using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RedditPodcastPoster.PodcastServices.YouTube.CandidatePreference;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.CandidatePreference;

public class YouTubeThumbnailMotionDetectorTests
{
    private const string VideoId = "S03n89L5YLA";
    private readonly StubHandler _handler = new();
    private readonly YouTubeCandidatePreferenceOptions _options = new() { ThumbnailFetchTimeoutSeconds = 1 };

    private YouTubeThumbnailMotionDetector Sut()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(YouTubeThumbnailMotionDetector.HttpClientName))
            .Returns(() => new HttpClient(_handler, disposeHandler: false));
        return new YouTubeThumbnailMotionDetector(
            factory.Object, Options.Create(_options), NullLogger<YouTubeThumbnailMotionDetector>.Instance);
    }

    [Fact(DisplayName = "Near-identical auto thumbnails (still-image upload) are Static.")]
    public async Task identical_frames_are_static()
    {
        Serve(Jpeg(seed: 1), Jpeg(seed: 1), Jpeg(seed: 1));
        (await Sut().DetectAsync(VideoId)).Should().Be(ThumbnailMotion.Static);
        _handler.Requested.Should().BeEquivalentTo(
            ["https://i.ytimg.com/vi/S03n89L5YLA/1.jpg", "https://i.ytimg.com/vi/S03n89L5YLA/2.jpg", "https://i.ytimg.com/vi/S03n89L5YLA/3.jpg"]);
    }

    [Fact(DisplayName = "Differing auto thumbnails (real video) are Moving.")]
    public async Task differing_frames_are_moving()
    {
        Serve(Jpeg(seed: 1), Jpeg(seed: 2), Jpeg(seed: 3));
        (await Sut().DetectAsync(VideoId)).Should().Be(ThumbnailMotion.Moving);
    }

    [Fact(DisplayName = "A missing auto thumbnail gives Unknown (fail-open).")]
    public async Task missing_thumbnail_is_unknown()
    {
        _handler.Respond = url => url.EndsWith("2.jpg")
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : Ok(Jpeg(seed: 1));
        (await Sut().DetectAsync(VideoId)).Should().Be(ThumbnailMotion.Unknown);
    }

    [Fact(DisplayName = "Uniform grey placeholder thumbnails give Unknown, not Static (fail-open).")]
    public async Task placeholder_is_unknown()
    {
        var grey = Jpeg(seed: null);
        Serve(grey, grey, grey);
        (await Sut().DetectAsync(VideoId)).Should().Be(ThumbnailMotion.Unknown);
    }

    [Fact(DisplayName = "Undecodable thumbnail bytes give Unknown.")]
    public async Task undecodable_is_unknown()
    {
        Serve([1, 2, 3], [1, 2, 3], [1, 2, 3]);
        (await Sut().DetectAsync(VideoId)).Should().Be(ThumbnailMotion.Unknown);
    }

    [Fact(DisplayName = "A network failure gives Unknown.")]
    public async Task network_failure_is_unknown()
    {
        _handler.Respond = _ => throw new HttpRequestException("down");
        (await Sut().DetectAsync(VideoId)).Should().Be(ThumbnailMotion.Unknown);
    }

    [Fact(DisplayName = "A fetch that exceeds ThumbnailFetchTimeoutSeconds gives Unknown.")]
    public async Task timeout_is_unknown()
    {
        _handler.Delay = TimeSpan.FromSeconds(10);
        Serve(Jpeg(seed: 1), Jpeg(seed: 1), Jpeg(seed: 1));
        (await Sut().DetectAsync(VideoId)).Should().Be(ThumbnailMotion.Unknown);
    }

    [Fact(DisplayName = "Thumbnails of different sizes give Unknown.")]
    public async Task mismatched_sizes_are_unknown()
    {
        Serve(Jpeg(seed: 1), Jpeg(seed: 1, width: 160), Jpeg(seed: 1));
        (await Sut().DetectAsync(VideoId)).Should().Be(ThumbnailMotion.Unknown);
    }

    [Fact(DisplayName = "The static threshold comes from options.")]
    public async Task threshold_is_configurable()
    {
        _options.StaticImageThreshold = 255;
        Serve(Jpeg(seed: 1), Jpeg(seed: 2), Jpeg(seed: 3));
        (await Sut().DetectAsync(VideoId)).Should().Be(ThumbnailMotion.Static);
    }

    private void Serve(byte[] one, byte[] two, byte[] three) =>
        _handler.Respond = url => Ok(url.EndsWith("1.jpg") ? one : url.EndsWith("2.jpg") ? two : three);

    private static HttpResponseMessage Ok(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };

    /// <summary>120x90 JPEG: a seeded random block pattern, or uniform grey when seed is null.</summary>
    private static byte[] Jpeg(int? seed, int width = 120)
    {
        using var image = new Image<Rgb24>(width, 90, new Rgb24(128, 128, 128));
        if (seed.HasValue)
        {
            var random = new Random(seed.Value);
            for (var by = 0; by < 90; by += 10)
            for (var bx = 0; bx < width; bx += 10)
            {
                var colour = new Rgb24((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
                for (var y = by; y < by + 10 && y < 90; y++)
                for (var x = bx; x < bx + 10 && x < width; x++)
                {
                    image[x, y] = colour;
                }
            }
        }

        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream);
        return stream.ToArray();
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