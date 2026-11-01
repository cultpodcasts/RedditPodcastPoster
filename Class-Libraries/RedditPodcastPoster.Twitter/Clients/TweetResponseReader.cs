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
    private const string ResourceNotFoundProblem = "https://api.twitter.com/2/problems/resource-not-found";
    private const string ResourceNotFoundProblemX = "https://api.x.com/2/problems/resource-not-found";

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

    /// <summary>Maps a <c>DELETE /2/tweets/:id</c> response. Only an explicit <c>deleted: true</c> counts as deleted.</summary>
    public static DeleteTweetState ReadDeleteState(HttpStatusCode statusCode, string? body)
    {
        if (statusCode == HttpStatusCode.NotFound)
        {
            return DeleteTweetState.NotFound;
        }

        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            return DeleteTweetState.TooManyRequests;
        }

        DeleteTweetResponseBody? parsed = null;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                parsed = JsonSerializer.Deserialize<DeleteTweetResponseBody>(body);
            }
            catch (JsonException)
            {
                parsed = null;
            }
        }

        if ((int) statusCode is >= 200 and < 300)
        {
            if (parsed?.Data?.Deleted == true)
            {
                return DeleteTweetState.Deleted;
            }

            if (parsed?.Errors?.Any(IsResourceNotFound) == true)
            {
                return DeleteTweetState.NotFound;
            }
        }

        return DeleteTweetState.Failed;
    }

    /// <summary>X post ids are 1–19 digits.</summary>
    public static bool IsTweetId(string? id) =>
        !string.IsNullOrEmpty(id) && id.Length <= 19 && id.All(char.IsAsciiDigit);

    private static bool IsResourceNotFound(DeleteTweetResponseError error) =>
        string.Equals(error.Type, ResourceNotFoundProblem, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(error.Type, ResourceNotFoundProblemX, StringComparison.OrdinalIgnoreCase);
}
