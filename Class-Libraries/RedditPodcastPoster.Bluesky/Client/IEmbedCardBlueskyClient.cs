using X.Bluesky;

namespace RedditPodcastPoster.Bluesky.Client;

public interface IEmbedCardBlueskyClient : IBlueskyClient
{
    /// <returns>AT URI of the created post (<c>at://…/app.bsky.feed.post/{rkey}</c>).</returns>
    Task<string> Post(string text, EmbedCardRequest embedCard);

    /// <returns>AT URI of the created post (<c>at://…/app.bsky.feed.post/{rkey}</c>).</returns>
    Task<string> Post(string text, EmbedCardRequest embedCard, string language);

    /// <returns>AT URI of the created post (<c>at://…/app.bsky.feed.post/{rkey}</c>).</returns>
    Task<string> Post(string text, string language);
}