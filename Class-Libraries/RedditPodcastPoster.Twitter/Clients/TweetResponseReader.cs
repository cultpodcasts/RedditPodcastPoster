using System.Net;
using System.Text.Json;
using RedditPodcastPoster.Twitter.Dtos;
using RedditPodcastPoster.Twitter.Models;

namespace RedditPodcastPoster.Twitter.Clients;

/// <summary>
/// Reads X API v2 response bodies for <see cref="TwitterClient"/>. Kept apart from the HTTP calls so the rules are
/// testable without a network, and so the client (also changed by the OAuth 2.0 work, #1201) only gains one-line calls.
/// </summary>
public static class TweetResponseReader
{
    /// <summary>
    /// The new post's id from a <c>POST /2/tweets</c> success body, or null when the body has none or is not JSON.
    /// Never throws: the post was made, so a bad body must not turn it into a failure.
    /// </summary>
    public static string? ReadPostedTweetId(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            var id = JsonSerializer.Deserialize<PostTweetResponseBody>(body)?.Data?.Id;
            return IsTweetId(id) ? id : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Maps a <c>DELETE /2/tweets/:id</c> response. Only an explicit <c>deleted: true</c> counts as deleted.
    /// NotFound (which lets the caller clear Tweeted) needs X to say so via <see cref="XApiProblem.IsResourceNotFound"/>.
    /// A bare 404 (a wrong URL, a proxy, an HTML page) is <see cref="DeleteTweetState.Failed"/>.
    /// </summary>
    public static DeleteTweetState ReadDeleteState(HttpStatusCode statusCode, string? body)
    {
        var problem = XApiProblem.Parse(body);

        if (statusCode == HttpStatusCode.TooManyRequests || problem.IsRateOrUsageLimited)
        {
            return DeleteTweetState.TooManyRequests;
        }

        if (statusCode == HttpStatusCode.NotFound)
        {
            return problem.IsResourceNotFound ? DeleteTweetState.NotFound : DeleteTweetState.Failed;
        }

        if ((int) statusCode is >= 200 and < 300)
        {
            if (ReadDeletedFlag(body) == true)
            {
                return DeleteTweetState.Deleted;
            }

            if (problem.IsResourceNotFound)
            {
                return DeleteTweetState.NotFound;
            }
        }

        return DeleteTweetState.Failed;
    }

    /// <summary>X post ids are 1–19 digits.</summary>
    public static bool IsTweetId(string? id) =>
        !string.IsNullOrEmpty(id) && id.Length <= 19 && id.All(char.IsAsciiDigit);

    private static bool? ReadDeletedFlag(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<DeleteTweetResponseBody>(body)?.Data?.Deleted;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
