using System.Net;

namespace RedditPodcastPoster.EdgeApi.Clients;

/// <summary>
/// Detects Cloudflare Bot Fight / "Just a moment…" challenge HTML so hero auto-promote can log an Exception (App Insights failure).
/// </summary>
public static class CloudflareChallengeResponse
{
    public const int MaxBodyCharsInLog = 500;

    public static bool LooksLikeBotChallenge(HttpStatusCode statusCode, string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        var text = body.AsSpan().TrimStart();
        if (text.Length == 0)
        {
            return false;
        }

        // Ordinary app JSON Forbidden / Unauthorized must not be treated as a bot challenge.
        if (text[0] is '{' or '[')
        {
            return false;
        }

        var haystack = body;
        if (ContainsIgnoreCase(haystack, "Just a moment") ||
            ContainsIgnoreCase(haystack, "cf-challenge") ||
            ContainsIgnoreCase(haystack, "cf-browser-verification") ||
            ContainsIgnoreCase(haystack, "Enable JavaScript and cookies to continue") ||
            ContainsIgnoreCase(haystack, "Checking your browser"))
        {
            return true;
        }

        // Forbidden/Unauthorized with non-JSON HTML-ish body is typically the edge challenge page.
        if (statusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized or HttpStatusCode.ServiceUnavailable)
        {
            return ContainsIgnoreCase(haystack, "<html") ||
                   ContainsIgnoreCase(haystack, "<!doctype html") ||
                   ContainsIgnoreCase(haystack, "cloudflare");
        }

        return false;
    }

    public static string TruncateBody(string? body, int maxChars = MaxBodyCharsInLog)
    {
        if (string.IsNullOrEmpty(body))
        {
            return string.Empty;
        }

        return body.Length <= maxChars
            ? body
            : body[..maxChars] + "…";
    }

    public static InvalidOperationException CreateException(HttpStatusCode statusCode, string? body)
    {
        var truncated = TruncateBody(body);
        return new InvalidOperationException(
            $"Cloudflare bot-mode challenge blocked AppendHeroEpisodes (status {(int)statusCode} {statusCode}). Body: {truncated}");
    }

    private static bool ContainsIgnoreCase(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
