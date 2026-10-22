using System.Net.Http.Headers;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;

namespace RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;

public class YouTubeThumbnailResolver(IHttpClientFactory httpClientFactory) : IYouTubeThumbnailResolver
{
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

            if (await IsUsableThumbnailAsync(candidate.Url, candidate.IsDefaultTier, cancellationToken))
            {
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

    public Task<bool> IsStoredImageUsableAsync(Uri imageUrl, CancellationToken cancellationToken = default)
    {
        return IsUsableThumbnailAsync(
            imageUrl,
            YouTubeThumbnailUrls.IsDefaultTier(imageUrl),
            cancellationToken);
    }

    private static bool HasUsableApiHeight(ThumbnailCandidate candidate)
    {
        return candidate.Height > 0 &&
               (candidate.IsDefaultTier || candidate.Height > YouTubeThumbnailValidation.PlaceholderMaxHeight);
    }

    private async Task<bool> IsUsableThumbnailAsync(
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
                return false;
            }

            var bytes = await ReadPrefixAsync(response.Content, cancellationToken);
            if (!JpegDimensionsReader.TryGetDimensions(bytes, out var width, out var height))
            {
                return false;
            }

            return YouTubeThumbnailValidation.IsUsableThumbnail(width, height, isDefaultTier);
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The probe's own timeout fired (HttpClient.Timeout). A slow tier is unusable, not a reason to abort
            // the caller: the next candidate is tried. A cancelled caller still propagates.
            return false;
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
