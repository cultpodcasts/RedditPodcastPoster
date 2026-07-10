using System.Net;
using System.Text.RegularExpressions;

namespace PeopleMigrator;

internal static partial class XProfileDisplayNameParser
{
    /// <summary>
    /// Parses public X/Twitter profile HTML for a display name.
    /// Works with SSR pages that expose og:title or &lt;title&gt; without login.
    /// </summary>
    public static string? ParseDisplayName(string html, string expectedUsername)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        foreach (var pattern in new[] { OgTitleContent(), TitleTag(), JsonLdName() })
        {
            var match = pattern.Match(html);
            if (!match.Success)
            {
                continue;
            }

            var name = match.Groups["name"].Value.Trim();
            if (IsLikelyLoginWall(name, expectedUsername))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                return WebUtility.HtmlDecode(name);
            }
        }

        return null;
    }

    private static bool IsLikelyLoginWall(string name, string expectedUsername)
    {
        if (name.Contains("Log in", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Sign up", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("X", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Twitter", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalizedExpected = PersonHandleNormalizer.ToMatchToken(expectedUsername);
        var normalizedName = PersonHandleNormalizer.ToMatchToken(name);
        return !string.IsNullOrWhiteSpace(normalizedExpected) &&
               !string.IsNullOrWhiteSpace(normalizedName) &&
               normalizedName.Equals(normalizedExpected, StringComparison.OrdinalIgnoreCase) &&
               !name.Contains(' ');
    }

    // og:title content="Anderson Cooper (@andersoncooper) on X"
    [GeneratedRegex(
        @"property=""og:title""\s+content=""(?<name>[^""]+?)\s+\(@[A-Za-z0-9_]+\)\s+on\s+X""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OgTitleContent();

    // <title>Anderson Cooper (@andersoncooper) / X</title>
    [GeneratedRegex(
        @"<title>\s*(?<name>[^<]+?)\s+\(@[A-Za-z0-9_]+\)\s*/\s*X\s*</title>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TitleTag();

    // Nitter: <title>Display Name (@handle) | nitter</title>
    [GeneratedRegex(
        @"<title>\s*(?<name>[^<|]+?)\s+\(@[A-Za-z0-9_]+\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JsonLdName();
}
