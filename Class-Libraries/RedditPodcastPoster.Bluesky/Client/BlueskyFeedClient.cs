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
    public async Task<string?> PostOpenGraphCard(string text, Uri url, string language)
    {
        var agent = await blueskyAgent.GetAsync();
        var post = new Post(text, langs: [language]);
        var card = await TryCreateCard(agent, url);
        if (card is not null)
        {
            post.Embed(card);
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

    private async Task<EmbeddedExternal?> TryCreateCard(BlueskyAgent agent, Uri url)
    {
        var details = await cardSource.TryGetAsync(url).ConfigureAwait(false);
        if (details is null)
        {
            return null;
        }

        BlueskyCardImage? image;
        try
        {
            image = await imageDownloader.Download(details.ImageUrl).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Bluesky card image download failed for '{url}'. Posting without a card.",
                url);
            return null;
        }

        if (image is null)
        {
            logger.LogWarning(
                "Bluesky card image download failed for '{url}'. Posting without a card.",
                url);
            return null;
        }

        var uploaded = await agent.UploadImage(image.Bytes, image.MimeType, details.Title, aspectRatio: null)
            .ConfigureAwait(false);
        if (!uploaded.Succeeded || uploaded.Result?.Image is null)
        {
            logger.LogWarning(
                "Bluesky card image upload failed for '{url}'. Status-code: {statusCode}. Posting without a card.",
                url,
                uploaded.StatusCode);
            return null;
        }

        return new EmbeddedExternal(details.Link.AbsoluteUri, details.Title, details.Description, uploaded.Result.Image);
    }
}
