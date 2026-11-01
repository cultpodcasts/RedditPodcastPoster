namespace RedditPodcastPoster.Twitter.Models;

/// <summary>Result of <c>DELETE /2/tweets/:id</c>.</summary>
public enum DeleteTweetState
{
    /// <summary>X answered <c>deleted: true</c>.</summary>
    Deleted = 1,

    /// <summary>X answered 404, or a resource-not-found problem: the post is already gone.</summary>
    NotFound,

    TooManyRequests,

    /// <summary>Anything else, including <c>deleted: false</c>. The post may still be live.</summary>
    Failed
}
