namespace RedditPodcastPoster.Twitter.Models;

/// <summary>
/// The item cannot form a valid X post. Callers record <see cref="Reason"/> on the document and skip
/// retries so the shortener is not rewritten every hour.
/// </summary>
public sealed class UntweetableException(string reason, string message) : Exception(message)
{
    /// <summary>Stable code from <see cref="TweetSkipReasons"/>.</summary>
    public string Reason { get; } = reason;
}
