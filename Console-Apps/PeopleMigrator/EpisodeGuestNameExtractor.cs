using System.Text.RegularExpressions;

namespace PeopleMigrator;

/// <summary>
/// Extracts guest/host display names and aliases from episode title and description text.
/// </summary>
internal static partial class EpisodeGuestNameExtractor
{
    public static IReadOnlyList<HandleNameExtraction> ExtractForEpisode(
        string? title,
        string? description,
        IEnumerable<string> twitterHandles,
        IEnumerable<string> blueskyHandles)
    {
        var handles = twitterHandles
            .Concat(blueskyHandles)
            .Select(PersonHandleNormalizer.NormalizeExactHandle)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();

        if (handles.Count == 0)
        {
            return [];
        }

        var guests = ParseRoleNames(description, "Guest").Concat(ParseTitleGuestNames(title)).ToList();
        var hosts = ParseRoleNames(description, "Host").ToList();

        var allCandidates = guests
            .Concat(hosts)
            .Select(NormalizePersonName)
            .Where(IsPlausiblePersonName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();

        var results = new List<HandleNameExtraction>();
        var assignedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var handle in handles)
        {
            var bestName = ChooseBestNameForHandle(handle, guests, hosts, allCandidates);
            if (string.IsNullOrWhiteSpace(bestName))
            {
                continue;
            }

            assignedNames.Add(bestName);
            var aliases = guests
                .Concat(hosts)
                .Where(x => !x.Equals(bestName, StringComparison.OrdinalIgnoreCase))
                .Where(x => NameRelatesToHandle(x, handle))
                .Where(IsPlausiblePersonName)
                .Where(x => PersonDisplayNameResolver.IsUsableDisplayName(x, handle, handle))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            results.Add(new HandleNameExtraction(handle, bestName, aliases));
        }

        return results;
    }

    internal static string? ChooseBestNameForHandle(
        string handle,
        IReadOnlyList<string> guests,
        IReadOnlyList<string> hosts,
        IReadOnlyList<string> allCandidates)
    {
        string? bestName = null;
        var bestScore = 0;

        foreach (var candidate in allCandidates)
        {
            if (!IsPlausiblePersonName(candidate) ||
                !PersonDisplayNameResolver.IsUsableDisplayName(candidate, handle, handle))
            {
                continue;
            }

            var score = ScoreNameAgainstHandle(candidate, handle);
            if (guests.Any(x => x.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            {
                score += 15;
            }

            if (hosts.Any(x => x.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            {
                score += 10;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestName = candidate;
            }
        }

        return bestScore >= 20 ? bestName : null;
    }

    internal static int ScoreNameAgainstHandle(string name, string? handle)
    {
        var handleToken = PersonHandleNormalizer.ToMatchToken(handle);
        if (string.IsNullOrWhiteSpace(handleToken))
        {
            return 0;
        }

        var parts = name
            .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizePersonName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(PersonHandleNormalizer.ToMatchToken)
            .Where(x => !string.IsNullOrWhiteSpace(x) && x.Length > 1)
            .Cast<string>()
            .ToList();

        if (parts.Count == 0)
        {
            return 0;
        }

        var joined = string.Concat(parts);
        if (handleToken.Contains(joined, StringComparison.OrdinalIgnoreCase) ||
            joined.Contains(handleToken, StringComparison.OrdinalIgnoreCase))
        {
            return 100 + parts.Count;
        }

        var matchedParts = parts.Count(part => handleToken.Contains(part, StringComparison.OrdinalIgnoreCase));
        var score = matchedParts * 25;

        if (parts.Count >= 2 && matchedParts >= 2)
        {
            score += 50;
        }

        return score;
    }

    internal static IEnumerable<string> ParseRoleNames(string? text, string role)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        foreach (Match match in RoleLinePattern(role).Matches(text))
        {
            var lineValue = match.Groups["names"].Value.Trim();
            foreach (var name in SplitNameList(lineValue))
            {
                var normalized = NormalizePersonName(name);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    yield return normalized;
                }
            }
        }
    }

    internal static IEnumerable<string> ParseTitleGuestNames(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            yield break;
        }

        foreach (Match match in TitleSaysPattern().Matches(title))
        {
            var normalized = NormalizePersonName(match.Groups["name"].Value);
            if (IsPlausiblePersonName(normalized))
            {
                yield return normalized!;
            }
        }

        foreach (Match match in TitleInterviewPattern().Matches(title))
        {
            foreach (var groupName in new[] { "left", "right" })
            {
                var normalized = NormalizePersonName(match.Groups[groupName].Value);
                if (IsPlausiblePersonName(normalized))
                {
                    yield return normalized!;
                }
            }
        }
    }

    internal static bool IsPlausiblePersonName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.Length > 60 ||
            value.Contains('@') ||
            value.Contains("http", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("twitter", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("episode recorded", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("podcast", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length is >= 2 and <= 5;
    }

    private static IEnumerable<string> SplitNameList(string value)
    {
        foreach (var part in NameListSplitPattern().Split(value))
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("and ", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[4..].Trim();
            }

            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                yield return trimmed;
            }
        }
    }

    internal static string? NormalizePersonName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Replace('\n', ' ').Replace('\r', ' ').Trim().TrimEnd('.', ',', ';', ':');
        trimmed = PossessiveSuffixPattern().Replace(trimmed, string.Empty).Trim();
        trimmed = PronounSuffixPattern().Replace(trimmed, string.Empty).Trim();
        trimmed = ParentheticalSocialHandlePattern().Replace(trimmed, string.Empty).Trim();
        trimmed = CredentialSuffixPattern().Replace(trimmed, string.Empty).Trim();
        trimmed = trimmed.Trim('"', '\'', '“', '”');

        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return string.Join(' ', trimmed.Split([' '], StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool NameRelatesToHandle(string name, string handle)
    {
        return ScoreNameAgainstHandle(name, handle) >= 25;
    }

    [GeneratedRegex(@"(?:^|\n)[^\n]*?\bGuest\s*:\s*(?<names>.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex GuestRoleLinePattern();

    [GeneratedRegex(@"(?:^|\n)[^\n]*?\bHost\s*:\s*(?<names>.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex HostRoleLinePattern();

    private static Regex RoleLinePattern(string role) =>
        role.Equals("Guest", StringComparison.OrdinalIgnoreCase) ? GuestRoleLinePattern() : HostRoleLinePattern();

    [GeneratedRegex(@"\bsays\s+(?<name>[A-Z][\w'.-]+(?:\s+[A-Z][\w'.-]+)+)", RegexOptions.IgnoreCase)]
    private static partial Regex TitleSaysPattern();

    [GeneratedRegex(@"(?<left>[A-Z][A-Za-z'.-]+(?:\s+[A-Z][A-Za-z'.-]+)(?:,\s*Ph\.?D\.?)?)\s+interviews\s+(?<right>[A-Z][A-Za-z'.-]+(?:\s+[A-Z][A-Za-z'.-]+)(?:,\s*Ph\.?D\.?)?)", RegexOptions.IgnoreCase)]
    private static partial Regex TitleInterviewPattern();

    [GeneratedRegex(@"\s*,\s*|\s+\band\b\s+", RegexOptions.IgnoreCase)]
    private static partial Regex NameListSplitPattern();

    [GeneratedRegex(@"(?:,\s*)?(?:Ph\.?\s?D\.?|MD|M\.?\s?D\.?|Esquire|Esq\.?|LCSW(?:-C)?|LMHC|LPC|LCPC|LISW)\.?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex CredentialSuffixPattern();

    [GeneratedRegex(@"['’]s\.?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex PossessiveSuffixPattern();

    [GeneratedRegex(@"\s*\((?:she|he|they)(?:/[^)]+)?\)\.?$", RegexOptions.IgnoreCase)]
    private static partial Regex PronounSuffixPattern();

    [GeneratedRegex(@"\s*\((?:@[^)]*|[^()]*(?:\.bsky\.social|\.social\b|twitter\.com|x\.com)[^)]*)\)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ParentheticalSocialHandlePattern();
}

internal readonly record struct HandleNameExtraction(
    string Handle,
    string DisplayName,
    IReadOnlyList<string> Aliases);
