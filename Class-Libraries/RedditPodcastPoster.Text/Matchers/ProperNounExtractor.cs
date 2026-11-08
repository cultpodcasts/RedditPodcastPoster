using System.Net;
using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Text.Matchers;

/// <summary>
/// Lightweight capitalized multi-word runs from episode <em>descriptions</em> for match-time catalogue scoring.
/// Titles are intentionally ignored — podcast/episode titles are often Title Case, so capitalized runs there
/// are noise rather than proper nouns. Not NER and not persisted.
/// </summary>
public static partial class ProperNounExtractor
{
    private static readonly HashSet<string> LeadingStopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "A", "An", "The", "And", "Or", "Of", "In", "On", "At", "To", "For", "With", "From", "By",
        "Dr", "Mr", "Mrs", "Ms", "Prof"
    };

    /// <summary>
    /// Extracts multi-word proper-noun phrases from a description (HTML-decoded).
    /// Single-token names are ignored (too noisy); leading stopwords like "The"/"Dr" are stripped from runs.
    /// </summary>
    public static IReadOnlySet<string> Extract(string? description)
    {
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddFromField(results, description);
        return results;
    }

    private static void AddFromField(HashSet<string> results, string? field)
    {
        var text = WebUtility.HtmlDecode(field ?? string.Empty);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        foreach (Match match in CapitalizedRun().Matches(text))
        {
            var tokens = match.Value
                .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => t.Length > 0)
                .ToList();
            while (tokens.Count > 0 && LeadingStopwords.Contains(tokens[0].TrimEnd('.')))
            {
                tokens.RemoveAt(0);
            }

            if (tokens.Count < 2)
            {
                continue;
            }

            var cleaned = tokens.Select(t => t.TrimEnd('.')).ToArray();
            // Full run plus consecutive subphrases (length ≥ 2) so "Journalist Gil Duran"
            // also yields "Gil Duran" for match-time overlap.
            for (var start = 0; start < cleaned.Length - 1; start++)
            {
                for (var length = 2; start + length <= cleaned.Length; length++)
                {
                    var phrase = string.Join(' ', cleaned.Skip(start).Take(length));
                    if (phrase.Length >= 3)
                    {
                        results.Add(phrase);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Shared proper-noun phrases between two descriptions, excluding any names already counted as subjects or guests.
    /// </summary>
    public static IReadOnlySet<string> Shared(
        string? leftDescription,
        string? rightDescription,
        IEnumerable<string>? exclude = null)
    {
        var excluded = new HashSet<string>(
            (exclude ?? []).Where(x => !string.IsNullOrWhiteSpace(x)),
            StringComparer.OrdinalIgnoreCase);
        var left = Extract(leftDescription);
        var right = Extract(rightDescription);
        return left
            .Where(x => right.Contains(x) && !excluded.Contains(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    [GeneratedRegex(
        @"\b(?:[A-Z][\p{L}'’]+(?:\s+[A-Z][\p{L}'’]+)+)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex CapitalizedRun();
}
