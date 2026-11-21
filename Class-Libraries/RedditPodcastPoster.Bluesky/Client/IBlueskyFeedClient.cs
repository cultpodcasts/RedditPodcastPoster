namespace RedditPodcastPoster.Bluesky.Client;

public interface IBlueskyFeedClient
{
    /// <summary>
    /// Creates a post. The card link is <paramref name="url"/>. When <paramref name="platformUrl"/>
    /// is set, the title, description, and image come from that YouTube or Spotify API URL.
    /// Otherwise <paramref name="url"/> is used for the API lookup. The short-url page is not fetched.
    /// </summary>
    /// <returns>The AT URI of the created post, or null when Bluesky did not create a record.</returns>
    Task<string?> PostOpenGraphCard(string text, Uri url, string language, Uri? platformUrl = null);

    /// <summary>
    /// As <see cref="PostOpenGraphCard(string, Uri, string, Uri?)"/>. When <paramref name="withoutCardImage"/> is true
    /// (a curator removed the episode's image, #1500) the card keeps the API title and description but carries no
    /// thumbnail, so the removed art is never posted.
    /// </summary>
    Task<string?> PostOpenGraphCard(string text, Uri url, string language, Uri? platformUrl, bool withoutCardImage);
}
