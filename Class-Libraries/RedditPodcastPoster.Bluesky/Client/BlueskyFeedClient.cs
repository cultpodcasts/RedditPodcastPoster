using idunno.AtProto;
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
    public Task<string?> PostOpenGraphCard(string text, Uri url, string language, Uri? platformUrl = null) =>
        PostOpenGraphCard(text, url, language, platformUrl, withoutCardImage: false);

    public async Task<string?> PostOpenGraphCard(
        string text,
        Uri url,
        string language,
        Uri? platformUrl,
        bool withoutCardImage)
    {
        var agent = await blueskyAgent.GetAsync();
        var post = new Post(text, langs: [language]);
        var card = await TryCreateApiCard(url, platformUrl ?? url, withoutCardImage).ConfigureAwait(false);
        var embed = await CreateCardEmbed(card, async (image, title) =>
        {
            var uploaded = await agent.UploadImage(image.Bytes, image.MimeType, title, aspectRatio: null)
                .ConfigureAwait(false);
            if (!uploaded.Succeeded || uploaded.Result?.Image is null)
            {
                logger.LogWarning(
                    "Bluesky card image upload failed for '{url}'. Status-code: {statusCode}. Posting without a card.",
                    url,
                    uploaded.StatusCode);
                return null;
            }

            return uploaded.Result.Image;
        }).ConfigureAwait(false);
        if (embed is not null)
        {
            post.Embed(embed);
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

    /// <summary>
    /// The external-link embed for <paramref name="card"/>. A card with no image (#1500: a curator removed the
    /// episode's image) is embedded with no thumbnail and nothing is uploaded. Otherwise the image is uploaded
    /// through <paramref name="uploadThumbnail"/>, and a failed upload (null) means no card.
    /// </summary>
    internal static async Task<EmbeddedExternal?> CreateCardEmbed(
        BlueskyApiCard? card,
        Func<BlueskyCardImage, string, Task<Blob?>> uploadThumbnail)
    {
        if (card is null)
        {
            return null;
        }

        if (card.Image is null)
        {
            return new EmbeddedExternal(card.Link.AbsoluteUri, card.Title, card.Description);
        }

        var thumbnail = await uploadThumbnail(card.Image, card.Title).ConfigureAwait(false);
        return thumbnail is null
            ? null
            : new EmbeddedExternal(card.Link.AbsoluteUri, card.Title, card.Description, thumbnail);
    }

    internal async Task<BlueskyApiCard?> TryCreateApiCard(
        Uri publicUrl,
        Uri platformUrl,
        bool withoutCardImage = false,
        CancellationToken cancellationToken = default)
    {
        var details = await cardSource.TryGetAsync(platformUrl, cancellationToken).ConfigureAwait(false);
        if (details is null)
        {
            return null;
        }

        if (withoutCardImage)
        {
            return new BlueskyApiCard(publicUrl, details.Title, details.Description, null);
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
