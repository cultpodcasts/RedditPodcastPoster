namespace PeopleMigrator;

/// <summary>
/// Matches display-name variants to a canonical person name or social handle.
/// </summary>
internal static class PersonNameMatcher
{
    private static readonly HashSet<string> IgnoredSuffixTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp", "phd", "md", "esq", "jr", "sr", "ii", "iii", "iv"
    };

    public static bool NameRelatesToPerson(
        string candidate,
        string canonicalName,
        string? twitterHandle,
        string? blueskyHandle)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(canonicalName))
        {
            return false;
        }

        if (PersonAliasFilter.IsSameName(candidate, canonicalName))
        {
            return true;
        }

        if (!IsLikelyAliasVariant(candidate, canonicalName))
        {
            return false;
        }

        var handleScore = Math.Max(
            EpisodeGuestNameExtractor.ScoreNameAgainstHandle(candidate, twitterHandle),
            EpisodeGuestNameExtractor.ScoreNameAgainstHandle(candidate, blueskyHandle));

        if (handleScore >= 25)
        {
            return true;
        }

        return FuzzyMatchCanonical(candidate, canonicalName);
    }

    internal static bool IsLikelyAliasVariant(string candidate, string canonicalName)
    {
        var candidateTokens = SignificantTokens(candidate);
        var canonicalTokens = SignificantTokens(canonicalName);

        if (candidateTokens.Count == 0 || canonicalTokens.Count == 0)
        {
            return false;
        }

        if (candidateTokens[0].Equals(canonicalTokens[0], StringComparison.OrdinalIgnoreCase))
        {
            var overlap = candidateTokens.Intersect(canonicalTokens, StringComparer.OrdinalIgnoreCase).Count();
            if (overlap >= 2)
            {
                return true;
            }

            if (candidateTokens.All(token => canonicalTokens.Contains(token, StringComparer.OrdinalIgnoreCase)))
            {
                return true;
            }

            return FuzzyMatchCanonical(candidate, canonicalName);
        }

        return FuzzyMatchCanonical(candidate, canonicalName);
    }

    internal static bool FuzzyMatchCoreTokens(IReadOnlyList<string> candidateTokens, IReadOnlyList<string> canonicalTokens)
    {
        if (candidateTokens.Count == 0 || canonicalTokens.Count == 0)
        {
            return false;
        }

        if (PersonAliasFilter.CoreTokenSetsEqual(candidateTokens, canonicalTokens))
        {
            return true;
        }

        var candidateJoined = string.Join(' ', candidateTokens);
        var canonicalJoined = string.Join(' ', canonicalTokens);

        if (candidateJoined.Contains(canonicalJoined, StringComparison.OrdinalIgnoreCase) ||
            canonicalJoined.Contains(candidateJoined, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var overlap = candidateTokens.Intersect(canonicalTokens, StringComparer.OrdinalIgnoreCase).Count();
        var shorterCount = Math.Min(candidateTokens.Count, canonicalTokens.Count);

        if (overlap >= 2 && overlap >= shorterCount)
        {
            return true;
        }

        if (overlap >= 2 && SharesFirstAndLast(candidateTokens, canonicalTokens))
        {
            return true;
        }

        if (candidateTokens.Count >= 2 &&
            canonicalTokens.Count >= 2 &&
            candidateTokens[^1].Equals(canonicalTokens[^1], StringComparison.OrdinalIgnoreCase) &&
            overlap >= 2)
        {
            return InitialsMatch(candidateTokens, canonicalTokens);
        }

        return false;
    }

    internal static bool FuzzyMatchCanonical(string candidate, string canonicalName)
    {
        var candidateCore = PersonAliasFilter.ExtractCoreTokens(candidate);
        var canonicalCore = PersonAliasFilter.ExtractCoreTokens(canonicalName);

        if (candidateCore.Count > 0 && canonicalCore.Count > 0 &&
            FuzzyMatchCoreTokens(candidateCore, canonicalCore))
        {
            return true;
        }

        var candidateTokens = SignificantTokens(candidate);
        var canonicalTokens = SignificantTokens(canonicalName);

        if (candidateTokens.Count == 0 || canonicalTokens.Count == 0)
        {
            return false;
        }

        if (PersonAliasFilter.IsSameName(candidate, canonicalName))
        {
            return true;
        }

        var candidateJoined = string.Join(' ', candidateTokens);
        var canonicalJoined = string.Join(' ', canonicalTokens);

        if (candidateJoined.Contains(canonicalJoined, StringComparison.OrdinalIgnoreCase) ||
            canonicalJoined.Contains(candidateJoined, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var overlap = candidateTokens.Intersect(canonicalTokens, StringComparer.OrdinalIgnoreCase).Count();
        var shorterCount = Math.Min(candidateTokens.Count, canonicalTokens.Count);

        if (overlap >= 2 && overlap >= shorterCount)
        {
            return true;
        }

        if (overlap >= 2 && SharesFirstAndLast(candidateTokens, canonicalTokens))
        {
            return true;
        }

        if (candidateTokens.Count >= 2 &&
            canonicalTokens.Count >= 2 &&
            candidateTokens[^1].Equals(canonicalTokens[^1], StringComparison.OrdinalIgnoreCase) &&
            overlap >= 2)
        {
            return InitialsMatch(candidateTokens, canonicalTokens);
        }

        return false;
    }

    internal static List<string> SignificantTokens(string name)
    {
        return EpisodeGuestNameExtractor.NormalizePersonName(name)?
            .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim('.'))
            .Where(x => x.Length > 0)
            .Where(x => x.Length > 1 || char.IsUpper(x[0]))
            .Where(x => !IgnoredSuffixTokens.Contains(x))
            .ToList() ?? [];
    }

    private static bool SharesFirstAndLast(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return false;
        }

        return left[0].Equals(right[0], StringComparison.OrdinalIgnoreCase) &&
               left[^1].Equals(right[^1], StringComparison.OrdinalIgnoreCase);
    }

    private static bool InitialsMatch(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count < 2 || right.Count < 2)
        {
            return false;
        }

        if (!left[0].Equals(right[0], StringComparison.OrdinalIgnoreCase) ||
            !left[^1].Equals(right[^1], StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var leftMiddle = left.Skip(1).Take(left.Count - 2).ToList();
        var rightMiddle = right.Skip(1).Take(right.Count - 2).ToList();

        if (leftMiddle.Count == 0 || rightMiddle.Count == 0)
        {
            return true;
        }

        foreach (var leftToken in leftMiddle)
        {
            if (rightMiddle.Any(rightToken => TokensEquivalent(leftToken, rightToken)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TokensEquivalent(string left, string right)
    {
        if (left.Equals(right, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (left.Length == 1 && right.StartsWith(left, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (right.Length == 1 && left.StartsWith(right, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
