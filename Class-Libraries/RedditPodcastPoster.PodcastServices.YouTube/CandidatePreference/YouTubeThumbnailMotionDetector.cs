using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RedditPodcastPoster.PodcastServices.YouTube.CandidatePreference;

/// <summary>
/// Compares the three small (120x90) auto thumbnails YouTube generates from points through a video.
/// A still-image upload yields near-identical frames. Never downloads video. Any failure, missing
/// thumbnail or blank/placeholder frame yields <see cref="ThumbnailMotion.Unknown"/> (fail-open).
/// </summary>
public class YouTubeThumbnailMotionDetector(
    IHttpClientFactory httpClientFactory,
    IOptions<YouTubeCandidatePreferenceOptions> options,
    ILogger<YouTubeThumbnailMotionDetector> logger) : IYouTubeThumbnailMotionDetector
{
    public const string HttpClientName = nameof(YouTubeThumbnailMotionDetector);

    private static readonly string[] AutoThumbnailNames = ["1.jpg", "2.jpg", "3.jpg"];

    public static Uri AutoThumbnailUrl(string videoId, string name) =>
        new($"https://i.ytimg.com/vi/{Uri.EscapeDataString(videoId)}/{name}");

    public async Task<ThumbnailMotion> DetectAsync(string videoId, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.ThumbnailFetchTimeoutSeconds));
        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            var frames = await Task.WhenAll(
                AutoThumbnailNames.Select(name => FetchFrame(client, AutoThumbnailUrl(videoId, name), timeout.Token)));
            if (frames.Any(f => f == null))
            {
                logger.LogDebug("Thumbnail motion unknown for '{videoId}': an auto thumbnail is missing.", videoId);
                return ThumbnailMotion.Unknown;
            }

            var decoded = frames.Select(f => f!).ToArray();
            if (decoded.Any(f => f.Width != decoded[0].Width || f.Height != decoded[0].Height) ||
                decoded.Any(f => f.PixelStandardDeviation() < settings.PlaceholderMaxPixelStdDev))
            {
                logger.LogDebug("Thumbnail motion unknown for '{videoId}': placeholder or mismatched thumbnails.", videoId);
                return ThumbnailMotion.Unknown;
            }

            var maxDifference = new[]
            {
                ThumbnailFrame.MeanAbsoluteDifference(decoded[0], decoded[1]),
                ThumbnailFrame.MeanAbsoluteDifference(decoded[1], decoded[2]),
                ThumbnailFrame.MeanAbsoluteDifference(decoded[0], decoded[2])
            }.Max();
            var motion = maxDifference < settings.StaticImageThreshold ? ThumbnailMotion.Static : ThumbnailMotion.Moving;
            logger.LogDebug(
                "Thumbnail motion for '{videoId}': {motion} (max frame difference {maxDifference:F2}, threshold {threshold:F2}).",
                videoId, motion, maxDifference, settings.StaticImageThreshold);
            return motion;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("Thumbnail motion unknown for '{videoId}': fetch timed out.", videoId);
            return ThumbnailMotion.Unknown;
        }
        catch (Exception ex) when (ex is HttpRequestException or ImageFormatException or UnknownImageFormatException or InvalidImageContentException)
        {
            logger.LogDebug(ex, "Thumbnail motion unknown for '{videoId}': fetch or decode failed.", videoId);
            return ThumbnailMotion.Unknown;
        }
    }

    private static async Task<ThumbnailFrame?> FetchFrame(HttpClient client, Uri url, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        using var image = Image.Load<Rgb24>(bytes);
        var rgb = new byte[image.Width * image.Height * 3];
        image.CopyPixelDataTo(rgb);
        return new ThumbnailFrame(image.Width, image.Height, rgb);
    }
}