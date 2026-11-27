namespace RedditPodcastPoster.Twitter.Models;

public enum TweetSendStatus
{
    Sent = 1,
    Failed,
    DuplicateForbidden,
    TooManyRequests,

    /// <summary>X answered 402 Payment Required: the account's API credits are depleted.</summary>
    PaymentRequired,

    /// <summary>X answered 401 Unauthorized, or a 403 Forbidden that is not a duplicate-content refusal.</summary>
    AuthenticationFailed,

    /// <summary>
    /// The body cannot be formed (title budget or missing link). The document was marked with a
    /// <c>tweetSkipReason</c> so later runs skip it.
    /// </summary>
    Untweetable,

    /// <summary>
    /// X answered 5xx. The post may or may not have been accepted, and the same outage will refuse later
    /// candidates, so the run stops. Posts are never auto-retried (#1205).
    /// </summary>
    ServerError,

    /// <summary>
    /// The request timed out before X answered. X may still have accepted the post, so the run stops and the
    /// post is not retried by this client (#1205).
    /// </summary>
    Timeout,

    /// <summary>
    /// X accepted the post but the document could not be saved as tweeted after retries. The post is live, so
    /// the run stops rather than posting the next candidate while state is unreliable (#1205).
    /// </summary>
    SentNotSaved
}
