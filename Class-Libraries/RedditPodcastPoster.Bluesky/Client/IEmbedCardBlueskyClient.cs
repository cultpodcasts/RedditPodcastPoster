using X.Bluesky;

namespace RedditPodcastPoster.Bluesky.Client;

public interface IEmbedCardBlueskyClient : IBlueskyClient
{
    Task Post(string text, EmbedCardRequest embedCard);
    Task Post(string text, EmbedCardRequest embedCard, string language);
    Task Post(string text, string language);
}