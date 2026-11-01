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
    private const string NotFoundErrorTitle = "Not Found Error";

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
    /// NotFound (which lets the caller clear Tweeted) needs X to say so: a <c>resource-not-found</c> problem or a
    /// <c>Not Found Error</c> title. A bare 404 (a wrong URL, a proxy, an HTML page) is <see cref="DeleteTweetState.Failed"/>.
    /// </summary>
    public static DeleteTweetState ReadDeleteState(HttpStatusCode statusCode, string? body)
    {
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

        if (statusCode == HttpStatusCode.NotFound)
        {
            return parsed != null && IsNotFoundProblem(parsed) ? DeleteTweetState.NotFound : DeleteTweetState.Failed;
        }

        if ((int) statusCode is >= 200 and < 300)
        {
            if (parsed?.Data?.Deleted == true)
            {
                return DeleteTweetState.Deleted;
            }

            if (parsed?.Errors?.Any(error => IsNotFound(error.Type, error.Title)) == true)
            {
                return DeleteTweetState.NotFound;
            }
        }

        return DeleteTweetState.Failed;
    }

    /// <summary>X post ids are 1–19 digits.</summary>
    public static bool IsTweetId(string? id) =>
        !string.IsNullOrEmpty(id) && id.Length <= 19 && id.All(char.IsAsciiDigit);

    private static bool IsNotFoundProblem(DeleteTweetResponseBody problem) =>
        IsNotFound(problem.Type, problem.Title) ||
        problem.Errors?.Any(error => IsNotFound(error.Type, error.Title)) == true;

    private static bool IsNotFound(string? type, string? title) =>
        string.Equals(type, ResourceNotFoundProblem, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, ResourceNotFoundProblemX, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(title, NotFoundErrorTitle, StringComparison.OrdinalIgnoreCase);
}
