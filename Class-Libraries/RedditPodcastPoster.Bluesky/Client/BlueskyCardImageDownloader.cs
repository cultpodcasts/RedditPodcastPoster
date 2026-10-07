namespace RedditPodcastPoster.Bluesky.Client;

public sealed class BlueskyCardImageDownloader : IBlueskyCardImageDownloader
{
    public const int MaxBytes = 1_000_000;

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    public async Task<BlueskyCardImage?> Download(Uri imageUrl, CancellationToken cancellationToken = default)
    {
        using var response = await Http.GetAsync(imageUrl, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes.Length is 0 or > MaxBytes)
        {
            return null;
        }

        var mime = response.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrWhiteSpace(mime) ||
            !mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            mime = "image/jpeg";
        }

        return new BlueskyCardImage(bytes, mime);
    }
}
