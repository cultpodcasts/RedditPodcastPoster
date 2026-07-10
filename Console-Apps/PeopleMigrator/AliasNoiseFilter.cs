namespace PeopleMigrator;

/// <summary>
/// Strips prose / episode-description artifacts from alias strings (duplicated phrases,
/// platform suffixes, location prefixes, sentence fragments).
/// </summary>
internal static class AliasNoiseFilter
{
    private static readonly HashSet<string> TrailingNoiseTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "books", "breaks", "down", "drew", "emotional", "facebook", "if", "instagram", "links",
        "mentioned", "music", "please", "substack", "twitter", "website", "youtube", "you"
    };

    private static readonly HashSet<string> LeadingNoiseTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "connection", "insider", "links", "mentioned", "stoke"
    };

    private static readonly string[] PhraseNoiseFragments =
    [
        "breaks down",
        "links mentioned",
        "connection links"
    ];

    public static bool IsNoiseAlias(string alias, string? canonicalName)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return true;
        }

        var trimmed = alias.Trim();
        if (HasDuplicatedPhrase(trimmed))
        {
            return true;
        }

        if (ContainsPhraseNoise(trimmed))
        {
            return true;
        }

        if (HasLeadingNoisePrefix(trimmed))
        {
            return true;
        }

        if (IsCanonicalWithNoiseAffixes(trimmed, canonicalName))
        {
            return true;
        }

        return false;
    }

    internal static bool HasDuplicatedPhrase(string alias)
    {
        var words = SplitWords(alias);
        if (words.Count < 4 || words.Count % 2 != 0)
        {
            return false;
        }

        var half = words.Count / 2;
        var first = string.Join(' ', words.Take(half));
        var second = string.Join(' ', words.Skip(half));
        return first.Equals(second, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool ContainsPhraseNoise(string alias)
    {
        var normalized = PersonAliasFilter.NormalizeForComparison(alias);
        return PhraseNoiseFragments.Any(fragment =>
            normalized.Contains(PersonAliasFilter.NormalizeForComparison(fragment), StringComparison.Ordinal));
    }

    internal static bool HasLeadingNoisePrefix(string alias)
    {
        var words = SplitWords(alias);
        if (words.Count == 0)
        {
            return false;
        }

        return LeadingNoiseTokens.Contains(words[0].Trim('\'')) ||
               (words.Count >= 2 && LeadingNoiseTokens.Contains(words[1].Trim('\'')));
    }

    internal static bool IsCanonicalWithNoiseAffixes(string alias, string? canonicalName)
    {
        if (string.IsNullOrWhiteSpace(canonicalName) ||
            canonicalName.Equals("Unknown guest", StringComparison.OrdinalIgnoreCase))
        {
            return EndsWithTrailingNoise(alias);
        }

        var stripped = StripNoiseAffixes(alias);
        if (PersonAliasFilter.WouldMatchCanonical(canonicalName, stripped))
        {
            return !PersonAliasFilter.IsSameName(stripped, alias);
        }

        if (HasLeadingPrefixBeforeCanonical(alias, canonicalName))
        {
            return true;
        }

        return EndsWithTrailingNoise(alias);
    }

    internal static bool HasLeadingPrefixBeforeCanonical(string alias, string canonicalName)
    {
        var aliasTokens = PersonAliasFilter.ExtractCoreTokens(alias);
        var canonicalTokens = PersonAliasFilter.ExtractCoreTokens(canonicalName);
        if (aliasTokens.Count <= canonicalTokens.Count || canonicalTokens.Count == 0)
        {
            return false;
        }

        var aliasCore = PersonAliasFilter.JoinCoreTokens(aliasTokens);
        var canonicalCore = PersonAliasFilter.JoinCoreTokens(canonicalTokens);
        if (!aliasCore.EndsWith(canonicalCore, StringComparison.Ordinal))
        {
            return false;
        }

        var prefixTokenCount = aliasTokens.Count - canonicalTokens.Count;
        if (prefixTokenCount <= 0)
        {
            return false;
        }

        var prefixTokens = aliasTokens.Take(prefixTokenCount).ToList();
        if (prefixTokens.Any(token => LeadingNoiseTokens.Contains(token)))
        {
            return true;
        }

        // Location-style prefixes (e.g. "Stoke Newington Diane Abbott") — extra tokens
        // before a canonical suffix that do not appear in the canonical name.
        var canonicalJoined = PersonAliasFilter.NormalizeForComparison(canonicalName);
        return prefixTokens.All(token =>
            !canonicalJoined.Contains(PersonAliasFilter.NormalizeForComparison(token), StringComparison.Ordinal));
    }

    internal static string StripNoiseAffixes(string alias)
    {
        var words = SplitWords(alias);
        while (words.Count > 1)
        {
            var last = words[^1].Trim('.').Trim('\'');
            var lastLower = last.ToLowerInvariant();
            if (TrailingNoiseTokens.Contains(lastLower) || lastLower is "dr" or "doctor")
            {
                words.RemoveAt(words.Count - 1);
                continue;
            }

            break;
        }

        while (words.Count > 1 && words[^1].EndsWith('\''))
        {
            words[^1] = words[^1].TrimEnd('\'').Trim();
            if (string.IsNullOrWhiteSpace(words[^1]))
            {
                words.RemoveAt(words.Count - 1);
            }
            else
            {
                break;
            }
        }

        return string.Join(' ', words);
    }

    private static bool EndsWithTrailingNoise(string alias)
    {
        var words = SplitWords(alias);
        if (words.Count == 0)
        {
            return false;
        }

        var last = words[^1].Trim('.').Trim('\'').ToLowerInvariant();
        return TrailingNoiseTokens.Contains(last) || last is "dr" or "doctor";
    }

    private static List<string> SplitWords(string value)
    {
        return value
            .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();
    }
}
