using System.Net;
using RedditPodcastPoster.Twitter.Models;

namespace RedditPodcastPoster.Twitter.Clients;

/// <summary>
/// Maps the status code of a failed <c>POST /2/tweets</c> (after the duplicate-content and rate-limit checks) to a
/// <see cref="TweetSendStatus"/>, so callers can tell account-wide failures (credits, credentials) from a failure
/// that only affects one post.
/// </summary>
public static class TweetSendFailureClassifier
{
    public static TweetSendStatus Classify(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.PaymentRequired => TweetSendStatus.PaymentRequired,
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => TweetSendStatus.AuthenticationFailed,
            _ => TweetSendStatus.Failed
        };
}
