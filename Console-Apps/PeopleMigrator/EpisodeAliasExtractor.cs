using System.Text.RegularExpressions;

namespace PeopleMigrator;

/// <summary>
/// Extracts alias variants for a known person from episode title and description text.
/// </summary>
internal static partial class EpisodeAliasExtractor
{
    private const int HandleProximityWindow = 100;

    private static readonly HashSet<string> SentenceFragmentWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "about", "after", "and", "arrested", "before", "could", "did", "does", "featuring", "follow", "from", "had",
        "has", "have", "how", "in", "inside", "introducing", "join", "meet", "might", "more", "not", "producer",
        "support", "the", "this", "today", "toward", "update", "was", "we", "what", "when", "where", "why", "will",
        "with", "would"
    };

    public static IReadOnlyList<string> ExtractAliases(
        string canonicalName,
        string? title,
        string? description,
        string? twitterHandle,
        string? blueskyHandle,
        IEnumerable<string> episodeHandles)
    {
        if (string.IsNullOrWhiteSpace(canonicalName))
        {
            return [];
        }

        var handles = episodeHandles
            .Select(PersonHandleNormalizer.NormalizeExactHandle)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();

        var normalizedTwitter = PersonHandleNormalizer.NormalizeExactHandle(twitterHandle);
        var normalizedBluesky = PersonHandleNormalizer.NormalizeExactHandle(blueskyHandle);
        var personHandles = new[] { normalizedTwitter, normalizedBluesky }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToList();

        var roleGuests = EpisodeGuestNameExtractor.ParseRoleNames(description, "Guest").ToList();
        var roleHosts = EpisodeGuestNameExtractor.ParseRoleNames(description, "Host").ToList();
        var titleNames = EpisodeGuestNameExtractor.ParseTitleGuestNames(title).ToList();
        var coGuestNames = BuildCoGuestNames(roleGuests, roleHosts, titleNames, handles, personHandles, canonicalName);

        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in roleGuests.Concat(roleHosts).Concat(titleNames))
        {
            candidates.Add(name);
        }

        foreach (var name in ExtractProseNames(title, description))
        {
            candidates.Add(name);
        }

        foreach (var name in ExtractNamesNearHandles(title, description, personHandles))
        {
            candidates.Add(name);
        }

        var aliases = new List<string>();
        foreach (var candidate in candidates)
        {
            var normalized = EpisodeGuestNameExtractor.NormalizePersonName(candidate);
            if (string.IsNullOrWhiteSpace(normalized) ||
                !EpisodeGuestNameExtractor.IsPlausiblePersonName(normalized) ||
                LooksLikeSentenceFragment(normalized) ||
                PersonAliasFilter.ShouldExcludeAlias(normalized, canonicalName, twitterHandle, blueskyHandle) ||
                IsCoGuestName(normalized, coGuestNames, canonicalName, twitterHandle, blueskyHandle) ||
                !PersonNameMatcher.NameRelatesToPerson(normalized, canonicalName, twitterHandle, blueskyHandle) ||
                !PersonDisplayNameResolver.IsUsableDisplayName(normalized, twitterHandle, blueskyHandle))
            {
                continue;
            }

            aliases.Add(normalized);
        }

        return aliases
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static HashSet<string> BuildCoGuestNames(
        IReadOnlyList<string> roleGuests,
        IReadOnlyList<string> roleHosts,
        IReadOnlyList<string> titleNames,
        IReadOnlyList<string> episodeHandles,
        IReadOnlyList<string> personHandles,
        string canonicalName)
    {
        var coGuests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allRoleNames = roleGuests.Concat(roleHosts).Concat(titleNames)
            .Select(EpisodeGuestNameExtractor.NormalizePersonName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var name in allRoleNames)
        {
            if (PersonNameMatcher.NameRelatesToPerson(name, canonicalName, personHandles.FirstOrDefault(), personHandles.Skip(1).FirstOrDefault()))
            {
                continue;
            }

            var bestHandle = FindBestHandleForName(name, episodeHandles);
            if (bestHandle == null)
            {
                coGuests.Add(name);
                continue;
            }

            if (!personHandles.Contains(bestHandle, StringComparer.OrdinalIgnoreCase))
            {
                coGuests.Add(name);
            }
        }

        return coGuests;
    }

    private static string? FindBestHandleForName(string name, IReadOnlyList<string> handles)
    {
        string? bestHandle = null;
        var bestScore = 0;

        foreach (var handle in handles)
        {
            var score = EpisodeGuestNameExtractor.ScoreNameAgainstHandle(name, handle);
            if (score > bestScore)
            {
                bestScore = score;
                bestHandle = handle;
            }
        }

        return bestScore >= 25 ? bestHandle : null;
    }

    private static bool LooksLikeSentenceFragment(string value)
    {
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return true;
        }

        if (SentenceFragmentWords.Contains(words[0]))
        {
            return true;
        }

        return words.Any(word => SentenceFragmentWords.Contains(word));
    }

    private static bool IsCoGuestName(
        string candidate,
        HashSet<string> coGuestNames,
        string canonicalName,
        string? twitterHandle,
        string? blueskyHandle)
    {
        if (!coGuestNames.Contains(candidate))
        {
            return false;
        }

        return !PersonNameMatcher.NameRelatesToPerson(candidate, canonicalName, twitterHandle, blueskyHandle);
    }

    private static IEnumerable<string> ExtractProseNames(string? title, string? description)
    {
        var text = string.Join("\n", new[] { title, description }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        foreach (Match match in ProseNamePattern().Matches(text))
        {
            var normalized = EpisodeGuestNameExtractor.NormalizePersonName(match.Groups["name"].Value);
            if (EpisodeGuestNameExtractor.IsPlausiblePersonName(normalized))
            {
                yield return normalized!;
            }
        }
    }

    private static IEnumerable<string> ExtractNamesNearHandles(
        string? title,
        string? description,
        IReadOnlyList<string> personHandles)
    {
        if (personHandles.Count == 0)
        {
            yield break;
        }

        var text = string.Join("\n", new[] { title, description }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        foreach (var handle in personHandles)
        {
            var localPart = PersonHandleNormalizer.ToMatchToken(handle);
            if (string.IsNullOrWhiteSpace(localPart))
            {
                continue;
            }

            foreach (Match match in HandleMentionPattern(localPart).Matches(text))
            {
                var start = Math.Max(0, match.Index - HandleProximityWindow);
                var window = text[start..match.Index];
                foreach (Match nameMatch in ProseNamePattern().Matches(window))
                {
                    var normalized = EpisodeGuestNameExtractor.NormalizePersonName(nameMatch.Groups["name"].Value);
                    if (EpisodeGuestNameExtractor.IsPlausiblePersonName(normalized))
                    {
                        yield return normalized!;
                    }
                }
            }
        }
    }

    private static Regex HandleMentionPattern(string localPart)
    {
        return new Regex(
            $@"(?<handle>@{Regex.Escape(localPart)}\b|@{Regex.Escape(localPart)}\.bsky\.social\b)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    [GeneratedRegex(@"\b(?<name>[A-Z][a-z]+(?:['-][A-Za-z]+)?(?:\s+(?:[A-Z]\.?|[A-Z][a-z]+(?:['-][A-Za-z]+)?)){1,4})\b")]
    private static partial Regex ProseNamePattern();
}
