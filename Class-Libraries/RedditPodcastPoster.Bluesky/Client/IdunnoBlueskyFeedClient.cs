using idunno.Bluesky;
using idunno.Bluesky.Embed;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.DependencyInjection;

namespace RedditPodcastPoster.Bluesky.Client;

public class IdunnoBlueskyFeedClient(
    IAsyncInstance<BlueskyAgent> blueskyAgent,
    ILogger<IdunnoBlueskyFeedClient> logger) : IBlueskyFeedClient
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
        try
        {
            var generator = agent.CreateOpenGraphEmbeddedCardGenerator();
            var card = await generator.Generate(url);
            if (card is null)
            {
                logger.LogWarning(
                    "Bluesky open-graph card was empty for '{url}'. Posting without a card.",
                    url);
            }

            return card;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Bluesky open-graph card generation failed for '{url}'. Posting without a card.",
                url);
            return null;
        }
    }
}
