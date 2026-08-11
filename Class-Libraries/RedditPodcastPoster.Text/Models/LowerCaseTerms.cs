using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.TitleCasing;

namespace RedditPodcastPoster.Text.Models;

/// <summary>
/// Builds newspaper-style mid-title lower-case regexes from word lists.
/// Default English seed words live here; runtime lists come from LookUps via
/// <c>ITitleCasingRulesProvider</c>.
/// </summary>
public static class LowerCaseTerms
{
    public static readonly string[] DefaultEnglishWords =
    [
        "the", "of", "on", "in", "to", "a", "an", "it", "not", "your", "you", "was", "isn't", "is", "want", "wants",
        "her", "his", "from", "their", "they", "out", "come", "coming", "away", "by", "what", "who", "made", "make",
        "since", "for", "go", "gone", "give", "gives", "given", "next", "with", "about", "how", "here", "called",
        "call", "doing", "do", "does", "where", "each", "other", "this", "after", "before", "be", "own", "more",
        "start", "my", "myself", "mine", "get", "gets", "up", "down", "meet", "met", "part", "parts", "ft", "at",
        "our", "us", "tell", "why", "don't", "tells", "when", "into", "vs", "only", "off", "end", "being", "re", "that",
        "talk", "are", "most", "we", "day", "or", "didn't", "know", "were", "as", "over", "its", "use", "one", "really",
        "work", "works", "worked", "did", "goes", "used", "has", "if", "just", "have", "all", "took", "no", "new", "old",
        "like", "etc"
    ];

    private static readonly string[] EnglishOrdinals = ["th", "st", "rd", "s", "nd"];

    private static readonly IDictionary<string, Regex> EmptyExpressions = new Dictionary<string, Regex>();

    /// <summary>English seed expressions (tests / CreateDefault). Prefer provider at runtime.</summary>
    public static readonly IDictionary<string, Regex> Expressions = BuildExpressions(DefaultEnglishWords, includeOrdinals: true);

    public static IDictionary<string, Regex> BuildExpressions(
        IEnumerable<string> words,
        bool includeOrdinals = false)
    {
        var result = new Dictionary<string, Regex>(StringComparer.OrdinalIgnoreCase);
        foreach (var word in words
                     .Select(w => w.Trim())
                     .Where(w => w.Length > 0)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var escaped = Regex.Escape(word);
            // "etc" and similar: always lower anywhere; others: mid-title lookbehinds
            if (string.Equals(word, "etc", StringComparison.OrdinalIgnoreCase))
            {
                result[word.ToLowerInvariant()] =
                    new Regex($@"\b{escaped}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }
            else
            {
                result[word.ToLowerInvariant()] = new Regex(
                    $@"(?<!^\s?)(?<!'\s?)(?<!""\s?)(?<!\(\s?)(?<!\-\s?)(?<!:\s?)(?<!\.\s?)(?<!\|\s?)(?<!\?\s?)(?<!\!\s?){escaped}\b",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }
        }

        if (includeOrdinals)
        {
            foreach (var ordinal in EnglishOrdinals)
            {
                result[ordinal] = new Regex($@"(?<=\d'?){ordinal}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }
        }

        return result;
    }

    public static IDictionary<string, Regex> ForLanguage(
        string? language,
        IReadOnlyDictionary<string, IReadOnlyList<string>> wordsByLanguage)
    {
        var key = NormaliseLanguageKey(language);
        if (!wordsByLanguage.TryGetValue(key, out var words) || words.Count == 0)
        {
            return EmptyExpressions;
        }

        return BuildExpressions(words, includeOrdinals: IsEnglish(language));
    }

    /// <summary>Null/whitespace and English IETF tags map to <c>en</c>. Preserves universal key <c>*</c>.</summary>
    public static string NormaliseLanguageKey(string? language)
    {
        if (!string.IsNullOrWhiteSpace(language) &&
            language.Trim() == TitleCasingRulesDocument.UniversalLanguageKey)
        {
            return TitleCasingRulesDocument.UniversalLanguageKey;
        }

        if (IsEnglish(language))
        {
            return "en";
        }

        var trimmed = language!.Trim().ToLowerInvariant().Replace('_', '-');
        var dash = trimmed.IndexOf('-');
        return dash > 0 ? trimmed[..dash] : trimmed;
    }

    public static bool IsEnglish(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return true;
        }

        if (language.Trim() == TitleCasingRulesDocument.UniversalLanguageKey)
        {
            return false;
        }

        var lower = language.Trim().ToLowerInvariant().Replace('_', '-');
        return lower is "en" || lower.StartsWith("en-", StringComparison.Ordinal);
    }
}
