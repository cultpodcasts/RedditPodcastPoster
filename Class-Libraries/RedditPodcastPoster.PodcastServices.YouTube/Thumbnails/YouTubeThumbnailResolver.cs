using RedditPodcastPoster.PodcastServices.YouTube.Extensions;

namespace RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;

public class YouTubeThumbnailResolver : IYouTubeThumbnailResolver
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public async Task<Uri?> GetImageUrlAsync(
        Google.Apis.YouTube.v3.Data.Video? video,
        CancellationToken cancellationToken = default)
    {
        foreach (var candidate in video.GetThumbnailCandidates())
        {
            if (await IsUsableThumbnailAsync(candidate, cancellationToken))
            {
                return candidate.Url;
            }
        }

        return null;
    }

    private static async Task<bool> IsUsableThumbnailAsync(
        ThumbnailCandidate candidate,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await HttpClient.GetAsync(
                candidate.Url,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (!JpegDimensionsReader.TryGetDimensions(bytes, out var width, out var height))
            {
                return false;
            }

            return YouTubeThumbnailValidation.IsUsableThumbnail(width, height, candidate.IsDefaultTier);
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
