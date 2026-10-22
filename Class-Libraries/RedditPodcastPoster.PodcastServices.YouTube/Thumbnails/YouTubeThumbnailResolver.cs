using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;

namespace RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;

public class YouTubeThumbnailResolver(
    IHttpClientFactory httpClientFactory,
    ILogger<YouTubeThumbnailResolver> logger) : IYouTubeThumbnailResolver
{
    /// <summary>Stable log prefix when a probe hits the probe client's own timeout.</summary>
    public const string ProbeTimeoutMessagePrefix = "YouTubeThumbnailProbeTimeout";

    private enum ProbeResult
    {
        Usable,
        Unusable,
        TimedOut
    }

    /// <summary>Named HttpClient used to probe thumbnail URLs. Registered with <see cref="ProbeTimeout"/>.</summary>
    public const string HttpClientName = nameof(YouTubeThumbnailResolver);

    /// <summary>Per-request timeout for a thumbnail probe.</summary>
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// A probe reads at most this many leading bytes (HTTP Range request, also enforced while reading in case the
    /// CDN ignores the range). A baseline JPEG's SOF marker, which carries the dimensions, sits well inside it.
    /// </summary>
    public const int ProbeMaxBytes = 32 * 1024;

    public async Task<Uri?> GetImageUrlAsync(
        Google.Apis.YouTube.v3.Data.Video? video,
        CancellationToken cancellationToken = default)
    {
        foreach (var candidate in video.GetThumbnailCandidates())
        {
            // The API already reports each tier's height: a non-default tier at or below the placeholder height
            // can never pass the probe, so it is skipped without a request.
            if (!HasUsableApiHeight(candidate))
            {
                continue;
            }

            var result = await ProbeAsync(candidate.Url, candidate.IsDefaultTier, cancellationToken);
            if (result == ProbeResult.Usable)
            {
                return candidate.Url;
            }

            if (result == ProbeResult.TimedOut && YouTubeThumbnailUrls.IsWithdrawableHighResolution(candidate.Url))
            {
                // A slow CDN says nothing about the tier: keep the API-listed maxresdefault/hq720 candidate (its height
                // already passed) rather than storing a lower tier for good. The recent-thumbnail re-check covers
                // exactly these tiers and replaces one later found withdrawn. Any other tier (e.g. sddefault, often the
                // 120x90 placeholder) is never re-checked, so a timeout there falls through to the next tier.
                logger.LogWarning(
                    ProbeTimeoutMessagePrefix +
                    " video-id='{VideoId}' url='{Url}' timeout-seconds='{TimeoutSeconds}' action='kept-api-candidate'",
                    video?.Id, candidate.Url, ProbeTimeout.TotalSeconds);
                return candidate.Url;
            }
        }

        return null;
    }

    public IReadOnlyList<Uri> GetUsableCandidateUrls(Google.Apis.YouTube.v3.Data.Video? video)
    {
        return video.GetThumbnailCandidates()
            .Where(HasUsableApiHeight)
            .Select(candidate => candidate.Url)
            .ToArray();
    }

    public async Task<bool> IsStoredImageUsableAsync(Uri imageUrl, CancellationToken cancellationToken = default)
    {
        var result = await ProbeAsync(
            imageUrl,
            YouTubeThumbnailUrls.IsDefaultTier(imageUrl),
            cancellationToken);
        if (result == ProbeResult.TimedOut)
        {
            // Never report a timeout as "withdrawn": the re-check would re-resolve on a slow CDN.
            throw new TimeoutException(
                $"{ProbeTimeoutMessagePrefix}: probe of '{imageUrl}' exceeded {ProbeTimeout.TotalSeconds}s.");
        }

        return result == ProbeResult.Usable;
    }

    private static bool HasUsableApiHeight(ThumbnailCandidate candidate)
    {
        return candidate.Height > 0 &&
               (candidate.IsDefaultTier || candidate.Height > YouTubeThumbnailValidation.PlaceholderMaxHeight);
    }

    private async Task<ProbeResult> ProbeAsync(
        Uri url,
        bool isDefaultTier,
        CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Range = new RangeHeaderValue(0, ProbeMaxBytes - 1);
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // A withdrawn tier answers 404 with a decodable placeholder body; never trust it.
                return ProbeResult.Unusable;
            }

            var bytes = await ReadPrefixAsync(response.Content, cancellationToken);
            if (!JpegDimensionsReader.TryGetDimensions(bytes, out var width, out var height))
            {
                return ProbeResult.Unusable;
            }

            return YouTubeThumbnailValidation.IsUsableThumbnail(width, height, isDefaultTier)
                ? ProbeResult.Usable
                : ProbeResult.Unusable;
        }
        catch (HttpRequestException)
        {
            return ProbeResult.Unusable;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The probe's own timeout fired (HttpClient.Timeout): the tier's state is unknown, not "unusable".
            // A cancelled caller still propagates.
            return ProbeResult.TimedOut;
        }
    }

    private static async Task<byte[]> ReadPrefixAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[ProbeMaxBytes];
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken);
            if (count == 0)
            {
                break;
            }

            read += count;
        }

        return buffer[..read];
    }
}
