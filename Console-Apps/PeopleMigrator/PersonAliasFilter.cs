using RedditPodcastPoster.Models;

namespace PeopleMigrator;

internal static class PersonAliasFilter
{
    private static readonly HashSet<string> HonorificPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "acting", "ag", "atty", "attorney", "congressman", "congresswoman", "dame", "democratic",
        "dr", "doctor", "gov", "governor", "hon", "honorable", "lady", "lord", "miss", "mr", "mrs",
        "ms", "nys", "prof", "professor", "rep", "representative", "sen", "senator", "sir"
    };

    private static readonly HashSet<string> CredentialSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "cla", "clc", "dba", "edd", "esq", "esquire", "ii", "iii", "iv", "jr", "lcsw", "lcsw-c",
        "lcswc", "lcpc", "lisw", "lmhc", "lpc", "ma", "md", "mp", "ms", "np", "ph", "phd", "rn", "sr"
    };

    public static bool IsSameName(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return NormalizeForComparison(left).Equals(NormalizeForComparison(right), StringComparison.Ordinal);
    }

    /// <summary>
    /// True when alias would resolve to the same person as canonical after stripping
    /// honorifics, credentials, and single-letter middle initials (e.g. Dr Alexandra Stein → Alexandra Stein).
    /// False for genuine nicknames (Alex Stein → Alexandra Stein) or extra middle names (Virginia Roberts Giuffre).
    /// </summary>
    public static bool WouldMatchCanonical(string? canonicalName, string alias)
    {
        if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(canonicalName))
        {
            return false;
        }

        if (IsSameName(alias, canonicalName))
        {
            return true;
        }

        var canonicalCore = CollapseInitials(ExtractCoreTokens(canonicalName));
        var aliasCore = CollapseInitials(ExtractCoreTokens(alias));

        if (canonicalCore.Count == 0 || aliasCore.Count == 0)
        {
            return false;
        }

        return CoreTokenSetsEqual(canonicalCore, aliasCore);
    }

    public static bool ShouldExcludeAlias(
        string alias,
        string? canonicalName,
        string? twitterHandle = null,
        string? blueskyHandle = null)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return true;
        }

        var trimmed = alias.Trim();

        if (!string.IsNullOrWhiteSpace(canonicalName) && WouldMatchCanonical(canonicalName, trimmed))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(canonicalName) &&
            !canonicalName.Equals("Unknown guest", StringComparison.OrdinalIgnoreCase))
        {
            var handleDerivedName = PersonHandleNormalizer.DeriveDisplayName(twitterHandle, blueskyHandle);
            if (!handleDerivedName.Equals("Unknown guest", StringComparison.OrdinalIgnoreCase) &&
                WouldMatchCanonical(handleDerivedName, trimmed))
            {
                return true;
            }
        }

        if (AliasNoiseFilter.IsNoiseAlias(trimmed, canonicalName))
        {
            return true;
        }

        return false;
    }

    public static string[] FilterAliases(
        IEnumerable<string>? aliases,
        string? canonicalName,
        string? twitterHandle = null,
        string? blueskyHandle = null)
    {
        if (aliases == null)
        {
            return [];
        }

        var filtered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in aliases)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                continue;
            }

            var trimmed = alias.Trim();
            if (ShouldExcludeAlias(trimmed, canonicalName, twitterHandle, blueskyHandle))
            {
                continue;
            }

            filtered.Add(trimmed);
        }

        return filtered.Count == 0
            ? []
            : filtered.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static string[] BuildAliasesForPerson(Person person)
    {
        return FilterAliases(person.Aliases, person.Name, person.TwitterHandle, person.BlueskyHandle);
    }

    internal static IReadOnlyList<string> ExtractCoreTokens(string name)
    {
        var normalized = EpisodeGuestNameExtractor.NormalizePersonName(name);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        var tokens = normalized
            .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim('.'))
            .Where(x => x.Length > 0)
            .ToList();

        while (tokens.Count > 0 && IsHonorificPrefix(tokens[0]))
        {
            tokens.RemoveAt(0);
        }

        while (tokens.Count > 0 && IsCredentialSuffix(tokens[^1]))
        {
            tokens.RemoveAt(tokens.Count - 1);
        }

        tokens = tokens
            .Where(x => x.Length > 1 || !char.IsLetter(x[0]))
            .ToList();

        return tokens;
    }

    internal static IReadOnlyList<string> CollapseInitials(IReadOnlyList<string> tokens)
    {
        return tokens.Where(x => x.Length > 1).ToList();
    }

    internal static bool CoreTokenSetsEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        return JoinCoreTokens(left).Equals(JoinCoreTokens(right), StringComparison.Ordinal);
    }

    internal static string JoinCoreTokens(IReadOnlyList<string> tokens)
    {
        return PersonHandleNormalizer.NormalizeLettersAndDigits(string.Join(' ', tokens));
    }

    internal static string NormalizeForComparison(string value)
    {
        var collapsed = CollapseWhitespace(value.Trim());
        return PersonHandleNormalizer.NormalizeLettersAndDigits(collapsed);
    }

    private static bool IsHonorificPrefix(string token)
    {
        return HonorificPrefixes.Contains(token.Trim('.'));
    }

    private static bool IsCredentialSuffix(string token)
    {
        return CredentialSuffixes.Contains(token.Trim('.'));
    }

    private static string CollapseWhitespace(string value)
    {
        return string.Join(' ', value.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries));
    }
}
