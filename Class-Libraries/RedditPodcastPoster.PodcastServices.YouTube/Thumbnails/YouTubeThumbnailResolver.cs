using RedditPodcastPoster.PodcastServices.YouTube.Extensions;

namespace RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;

public class YouTubeThumbnailResolver(IHttpClientFactory httpClientFactory) : IYouTubeThumbnailResolver
{
    /// <summary>Named HttpClient used to probe thumbnail URLs. Registered with <see cref="ProbeTimeout"/>.</summary>
    public const string HttpClientName = nameof(YouTubeThumbnailResolver);

    /// <summary>Per-request timeout for a thumbnail probe.</summary>
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(15);

    public async Task<Uri?> GetImageUrlAsync(
        Google.Apis.YouTube.v3.Data.Video? video,
        CancellationToken cancellationToken = default)
    {
        foreach (var candidate in video.GetThumbnailCandidates())
        {
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
            .Where(candidate =>
                candidate.Height > 0 &&
                (candidate.IsDefaultTier || candidate.Height > YouTubeThumbnailValidation.PlaceholderMaxHeight))
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

    private async Task<bool> IsUsableThumbnailAsync(
        Uri url,
        bool isDefaultTier,
        CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = httpClientFactory.CreateClient(HttpClientName);
            using var response = await httpClient.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // A withdrawn tier answers 404 with a decodable placeholder body; never trust it.
                return false;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
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
        catch (TaskCanceledException)
        {
            throw;
        }
    }
}
