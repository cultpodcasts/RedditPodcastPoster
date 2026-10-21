namespace RedditPodcastPoster.Bluesky.Client;

public sealed class BlueskyCardImageDownloader : IBlueskyCardImageDownloader
{
    public const int MaxBytes = 1_000_000;

    private readonly HttpClient _http;

    public BlueskyCardImageDownloader()
        : this(new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
    {
    }

    internal BlueskyCardImageDownloader(HttpMessageHandler handler)
        : this(new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(20) })
    {
    }

    private BlueskyCardImageDownloader(HttpClient http)
    {
        _http = http;
    }

    public async Task<BlueskyCardImage?> Download(
        IReadOnlyList<Uri> imageUrls,
        CancellationToken cancellationToken = default)
    {
        if (imageUrls.Count == 0)
        {
            return null;
        }

        foreach (var imageUrl in imageUrls)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var image = await DownloadOne(imageUrl, cancellationToken).ConfigureAwait(false);
                if (image is not null)
                {
                    return image;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }
        }

        return null;
    }

    private async Task<BlueskyCardImage?> DownloadOne(Uri imageUrl, CancellationToken cancellationToken)
    {
        using var response = await _http
            .GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var mime = response.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrWhiteSpace(mime) ||
            !mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var buffer = new byte[MaxBytes + 1];
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken).ConfigureAwait(false);
            if (n == 0)
            {
                break;
            }

            read += n;
        }

        if (read is 0 or > MaxBytes)
        {
            return null;
        }

        var bytes = new byte[read];
        buffer.AsSpan(0, read).CopyTo(bytes);
        return new BlueskyCardImage(bytes, mime);
    }
}
