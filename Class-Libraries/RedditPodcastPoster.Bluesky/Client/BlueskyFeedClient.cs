using idunno.Bluesky;
using idunno.Bluesky.Embed;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.DependencyInjection;

namespace RedditPodcastPoster.Bluesky.Client;

public class BlueskyFeedClient(
    IAsyncInstance<BlueskyAgent> blueskyAgent,
    IBlueskyPlatformCardSource cardSource,
    IBlueskyCardImageDownloader imageDownloader,
    ILogger<BlueskyFeedClient> logger) : IBlueskyFeedClient
{
    public async Task<string?> PostOpenGraphCard(string text, Uri url, string language, Uri? platformUrl = null)
    {
        var agent = await blueskyAgent.GetAsync();
        var post = new Post(text, langs: [language]);
        var card = await TryCreateApiCard(url, platformUrl ?? url).ConfigureAwait(false);
        if (card is not null)
        {
            var uploaded = await agent.UploadImage(card.Image.Bytes, card.Image.MimeType, card.Title, aspectRatio: null)
                .ConfigureAwait(false);
            if (!uploaded.Succeeded || uploaded.Result?.Image is null)
            {
                logger.LogWarning(
                    "Bluesky card image upload failed for '{url}'. Status-code: {statusCode}. Posting without a card.",
                    url,
                    uploaded.StatusCode);
            }
            else
            {
                post.Embed(new EmbeddedExternal(
                    card.Link.AbsoluteUri,
                    card.Title,
                    card.Description,
                    uploaded.Result.Image));
            }
        }

        var result = await agent.Post(post);
        if (!result.Succeeded || result.Result?.StrongReference is null)
        {
            logger.LogError(
                "Bluesky post failed. Status-code: {statusCode}, error-detail-error: {errorDetailError}, error-detail-message: {errorDetailMessage}.",
                result.StatusCode,
                result.AtErrorDetail?.Error,
                result.AtErrorDetail?.Message);
            return null;
        }

        return result.Result.StrongReference.Uri.ToString();
    }

    internal async Task<BlueskyApiCard?> TryCreateApiCard(
        Uri publicUrl,
        Uri platformUrl,
        CancellationToken cancellationToken = default)
    {
        var details = await cardSource.TryGetAsync(platformUrl, cancellationToken).ConfigureAwait(false);
        if (details is null)
        {
            return null;
        }

        BlueskyCardImage? image;
        try
        {
            image = await imageDownloader.Download(details.ImageUrls, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Bluesky card image download failed for '{url}'. Posting without a card.",
                publicUrl);
            return null;
        }

        if (image is null)
        {
            logger.LogWarning(
                "Bluesky card image download failed for '{url}'. Posting without a card.",
                publicUrl);
            return null;
        }

        return new BlueskyApiCard(publicUrl, details.Title, details.Description, image);
    }
}
