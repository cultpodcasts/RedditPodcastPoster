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
    AuthenticationFailed
}
