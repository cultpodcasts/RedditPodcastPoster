using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Text.Models;

namespace RedditPodcastPoster.Text.TitleCasing;

public class TitleCasingRulesProvider(
    IReadOnlyDictionary<string, LanguageTitleCasingRulesDocument> byLanguage)
    : ITitleCasingRulesProvider
{
    private readonly Dictionary<string, IDictionary<string, Regex>> _lowerCache = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, LanguageTitleCasingRulesDocument> GetAll() => byLanguage;

    public IDictionary<string, Regex> GetLowerCaseExpressions(string? language)
    {
        var key = LowerCaseTerms.NormaliseLanguageKey(language);
        if (_lowerCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (!byLanguage.TryGetValue(key, out var rules) || rules.LowerCaseTerms.Count == 0)
        {
            _lowerCache[key] = new Dictionary<string, Regex>();
            return _lowerCache[key];
        }

        var built = LowerCaseTerms.BuildExpressions(
            rules.LowerCaseTerms,
            includeOrdinals: LowerCaseTerms.IsEnglish(language));
        _lowerCache[key] = built;
        return built;
    }

    public IReadOnlyList<KnownTermEntry> GetKnownTerms(string? language)
    {
        var key = LowerCaseTerms.NormaliseLanguageKey(language);
        if (key == LanguageTitleCasingRulesDocument.UniversalLanguageKey ||
            !byLanguage.TryGetValue(key, out var rules))
        {
            return [];
        }

        return rules.KnownTerms;
    }

    public IReadOnlyList<KnownTermEntry> GetUniversalKnownTerms()
    {
        if (!byLanguage.TryGetValue(LanguageTitleCasingRulesDocument.UniversalLanguageKey, out var rules))
        {
            return [];
        }

        return rules.KnownTerms;
    }
}
