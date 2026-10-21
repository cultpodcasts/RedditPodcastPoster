using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace RedditPodcastPoster.PodcastServices.YouTube.CandidatePreference;

/// <summary>
/// Compares the three small (120x90) auto thumbnails YouTube generates from points through a video.
/// A still-image upload yields near-identical frames. Decoded with SkiaSharp. Never downloads video. Reads at most
/// <see cref="YouTubeCandidatePreferenceOptions.MaxThumbnailBytes"/> and follows at most
/// <see cref="YouTubeCandidatePreferenceOptions.MaxThumbnailRedirects"/> redirects. Any failure,
/// missing thumbnail, over-cap body or blank/placeholder frame yields <see cref="ThumbnailMotion.Unknown"/>
/// (fail-open). Caller cancellation is propagated.
/// </summary>
public class YouTubeThumbnailMotionDetector(
    IHttpClientFactory httpClientFactory,
    IOptions<YouTubeCandidatePreferenceOptions> options,
    ILogger<YouTubeThumbnailMotionDetector> logger) : IYouTubeThumbnailMotionDetector
{
    public const string HttpClientName = nameof(YouTubeThumbnailMotionDetector);

    /// <summary>Read size for a capped thumbnail body. Not a tunable; the ceiling is <see cref="YouTubeCandidatePreferenceOptions.MaxThumbnailBytes"/>.</summary>
    private const int ReadChunkBytes = 8192;

    private static readonly string[] AutoThumbnailNames = ["1.jpg", "2.jpg", "3.jpg"];

    public static Uri AutoThumbnailUrl(string videoId, string name) =>
        new($"https://i.ytimg.com/vi/{Uri.EscapeDataString(videoId)}/{name}");

    /// <summary>Applies the thumbnail byte ceiling to a named <see cref="HttpClient"/> before its first request.</summary>
    internal static void ApplyClientLimits(HttpClient client, YouTubeCandidatePreferenceOptions settings) =>
        client.MaxResponseContentBufferSize = settings.MaxThumbnailBytes;

    /// <summary>Follows a few redirects (image-host hops) and then fails, rather than the platform default of 50.</summary>
    internal static HttpClientHandler CreateThumbnailHandler(YouTubeCandidatePreferenceOptions settings) =>
        new()
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = settings.MaxThumbnailRedirects
        };

    public async Task<ThumbnailMotion> DetectAsync(string videoId, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.ThumbnailFetchTimeoutSeconds));
        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            var frames = await Task.WhenAll(
                AutoThumbnailNames.Select(name => FetchFrame(
                    client, AutoThumbnailUrl(videoId, name), settings.MaxThumbnailBytes, timeout.Token)));
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Thumbnail motion unknown for '{videoId}': fetch timed out.", videoId);
            return ThumbnailMotion.Unknown;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Thumbnail motion unknown for '{videoId}': fetch or decode failed.", videoId);
            return ThumbnailMotion.Unknown;
        }
    }

    private async Task<ThumbnailFrame?> FetchFrame(
        HttpClient client, Uri url, int maxBytes, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        if (response.Content.Headers.ContentLength is long advertised && advertised > maxBytes)
        {
            logger.LogDebug(
                "Thumbnail '{url}' advertised {advertised} bytes, above the {maxBytes} byte ceiling.",
                url, advertised, maxBytes);
            return null;
        }

        var bytes = await ReadAtMostAsync(response.Content, maxBytes, cancellationToken);
        if (bytes == null)
        {
            logger.LogDebug("Thumbnail '{url}' exceeded the {maxBytes} byte ceiling.", url, maxBytes);
            return null;
        }

        return DecodeFrame(bytes, maxBytes, url);
    }

    /// <summary>
    /// Reads image dimensions before allocating a bitmap. A frame whose decoded RGB buffer would exceed
    /// <paramref name="maxBytes"/> is rejected. Channel order is RGBA so Linux and Windows agree.
    /// </summary>
    private ThumbnailFrame? DecodeFrame(byte[] bytes, int maxBytes, Uri url)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec == null)
        {
            return null;
        }

        var width = codec.Info.Width;
        var height = codec.Info.Height;
        if (width <= 0 || height <= 0 ||
            !TryDecodedByteCount(width, height, out var decodedBytes) ||
            decodedBytes > maxBytes)
        {
            logger.LogDebug(
                "Thumbnail '{url}' decoded above the {maxBytes} byte ceiling.",
                url, maxBytes);
            return null;
        }

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        if (codec.GetPixels(info, bitmap.GetPixels()) != SKCodecResult.Success)
        {
            return null;
        }

        var rgb = new byte[decodedBytes];
        CopyRgb(bitmap, rgb);
        return new ThumbnailFrame(width, height, rgb);
    }

    private static void CopyRgb(SKBitmap bitmap, byte[] rgb)
    {
        var pixels = bitmap.GetPixelSpan();
        var width = bitmap.Width;
        var rowBytes = bitmap.RowBytes;
        for (var y = 0; y < bitmap.Height; y++)
        {
            var rowStart = y * rowBytes;
            for (var x = 0; x < width; x++)
            {
                var src = rowStart + (x * 4);
                var dst = ((y * width) + x) * 3;
                rgb[dst] = pixels[src];
                rgb[dst + 1] = pixels[src + 1];
                rgb[dst + 2] = pixels[src + 2];
            }
        }
    }

    private static bool TryDecodedByteCount(int width, int height, out int decodedBytes)
    {
        decodedBytes = 0;
        try
        {
            var bytes = checked((long)width * height * 3);
            if (bytes > int.MaxValue)
            {
                return false;
            }

            decodedBytes = (int)bytes;
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    /// <summary>Copies the body into a buffer that never grows past <paramref name="maxBytes"/>.</summary>
    private static async Task<byte[]?> ReadAtMostAsync(HttpContent content, int maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[ReadChunkBytes];
        while (true)
        {
            var read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }
    }
}
