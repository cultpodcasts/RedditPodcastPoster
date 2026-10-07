namespace RedditPodcastPoster.Bluesky.Client;

public interface IBlueskyFeedClient
{
    /// <summary>
    /// Creates a post with an Open Graph card for <paramref name="url"/> when the page provides one.
    /// </summary>
    /// <returns>The AT URI of the created post, or null when Bluesky did not create a record.</returns>
    Task<string?> PostOpenGraphCard(string text, Uri url, string language);
}
