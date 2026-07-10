using System.Text.RegularExpressions;

namespace PeopleMigrator;

internal static partial class PersonHandleNormalizer
{
    public static IEnumerable<string> SplitHandleField(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (var token in Whitespace().Split(value.Trim()))
        {
            if (string.IsNullOrWhiteSpace(token) || token.StartsWith('#'))
            {
                continue;
            }

            if (IsHandleToken(token))
            {
                yield return token;
            }
        }
    }

    public static IEnumerable<string> ExpandHandles(IEnumerable<string>? handles)
    {
        if (handles == null)
        {
            yield break;
        }

        foreach (var field in handles)
        {
            foreach (var handle in SplitHandleField(field))
            {
                yield return handle;
            }
        }
    }

    public static string? NormalizeExactHandle(string? handle)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            return null;
        }

        var trimmed = handle.Trim();
        return trimmed.StartsWith('@') ? trimmed.ToLowerInvariant() : $"@{trimmed}".ToLowerInvariant();
    }

    /// <summary>
    /// Comparable token for cross-platform matching (twitter local part vs bsky local part only).
    /// </summary>
    public static string? ToMatchToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var local = ExtractLocalPart(value);
        if (string.IsNullOrWhiteSpace(local))
        {
            return null;
        }

        return LettersAndDigitsOnly(local);
    }

    public static string DeriveDisplayName(string? twitter, string? bluesky)
    {
        var twitterLocal = ExtractTwitterLocalPart(twitter);
        if (!string.IsNullOrWhiteSpace(twitterLocal))
        {
            return ToTitleCaseFromHandle(twitterLocal);
        }

        var blueskyLocal = ExtractBlueskyLocalPart(bluesky);
        if (!string.IsNullOrWhiteSpace(blueskyLocal))
        {
            return ToTitleCaseFromHandle(blueskyLocal);
        }

        return "Unknown guest";
    }

    private static bool IsHandleToken(string token)
    {
        var trimmed = token.Trim().TrimStart('@');
        return !string.IsNullOrWhiteSpace(trimmed) && HandleToken().IsMatch(trimmed);
    }

    private static string? ExtractLocalPart(string value)
    {
        var blueskyLocal = ExtractBlueskyLocalPart(value);
        if (!string.IsNullOrWhiteSpace(blueskyLocal))
        {
            return blueskyLocal;
        }

        return ExtractTwitterLocalPart(value);
    }

    private static string? ExtractTwitterLocalPart(string? handle)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            return null;
        }

        var trimmed = handle.Trim().TrimStart('@');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        var first = trimmed.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(first) ? null : first;
    }

    private static string? ExtractBlueskyLocalPart(string? handle)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            return null;
        }

        var trimmed = handle.Trim().TrimStart('@');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        var first = trimmed.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(first))
        {
            return null;
        }

        const string bskySuffix = ".bsky.social";
        if (first.EndsWith(bskySuffix, StringComparison.OrdinalIgnoreCase))
        {
            return first[..^bskySuffix.Length];
        }

        var dotIndex = first.IndexOf('.');
        return dotIndex > 0 ? first[..dotIndex] : first;
    }

    private static string LettersAndDigitsOnly(string value)
    {
        return NonAlphaNumeric().Replace(value, string.Empty).ToLowerInvariant();
    }

    /// <summary>
    /// Normalize an entire display name for handle comparison (all words, not just the first token).
    /// </summary>
    public static string NormalizeLettersAndDigits(string value)
    {
        return LettersAndDigitsOnly(value);
    }

    private static string ToTitleCaseFromHandle(string localPart)
    {
        if (localPart.Contains('_') || localPart.Contains('-'))
        {
            return string.Join(' ',
                localPart.Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => char.ToUpperInvariant(x[0]) + x[1..].ToLowerInvariant()));
        }

        return localPart;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9._-]*$")]
    private static partial Regex HandleToken();

    [GeneratedRegex("[^a-zA-Z0-9]")]
    private static partial Regex NonAlphaNumeric();
}
