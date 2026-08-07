using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Text.Models;

namespace RedditPodcastPoster.Text.TitleCasing;

public class TitleCasingRulesProvider : ITitleCasingRulesProvider
{
    private readonly IReadOnlyDictionary<string, LanguageTitleCasingRulesDocument> _byLanguage;

    // Homepage (and other) sanitise runs titles in parallel; caches must be concurrency-safe.
    private readonly ConcurrentDictionary<string, IDictionary<string, Regex>> _lowerCache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, IReadOnlyList<KnownTermReplacement>> _knownTermCache =
        new(StringComparer.OrdinalIgnoreCase);

    public TitleCasingRulesProvider(IReadOnlyDictionary<string, LanguageTitleCasingRulesDocument> byLanguage)
    {
        _byLanguage = byLanguage;
        // Eager compile for English + universal — the common homepage path.
        _ = GetLowerCaseExpressions("en");
        _ = GetUniversalKnownTermReplacements();
        _ = GetKnownTermReplacements("en");
    }

    public IReadOnlyDictionary<string, LanguageTitleCasingRulesDocument> GetAll() => _byLanguage;

    public IDictionary<string, Regex> GetLowerCaseExpressions(string? language)
    {
        var key = LowerCaseTerms.NormaliseLanguageKey(language);
        return _lowerCache.GetOrAdd(key, BuildLowerCaseExpressions);
    }

    public IReadOnlyList<KnownTermEntry> GetKnownTerms(string? language)
    {
        var key = LowerCaseTerms.NormaliseLanguageKey(language);
        if (key == LanguageTitleCasingRulesDocument.UniversalLanguageKey ||
            !_byLanguage.TryGetValue(key, out var rules))
        {
            return [];
        }

        return rules.KnownTerms;
    }

    public IReadOnlyList<KnownTermEntry> GetUniversalKnownTerms()
    {
        if (!_byLanguage.TryGetValue(LanguageTitleCasingRulesDocument.UniversalLanguageKey, out var rules))
        {
            return [];
        }

        return rules.KnownTerms;
    }

    public IReadOnlyList<KnownTermReplacement> GetKnownTermReplacements(string? language)
    {
        var key = LowerCaseTerms.NormaliseLanguageKey(language);
        if (key == LanguageTitleCasingRulesDocument.UniversalLanguageKey)
        {
            return [];
        }

        return _knownTermCache.GetOrAdd(key, BuildKnownTermReplacements);
    }

    public IReadOnlyList<KnownTermReplacement> GetUniversalKnownTermReplacements() =>
        _knownTermCache.GetOrAdd(
            LanguageTitleCasingRulesDocument.UniversalLanguageKey,
            BuildKnownTermReplacements);

    private IDictionary<string, Regex> BuildLowerCaseExpressions(string key)
    {
        if (!_byLanguage.TryGetValue(key, out var rules) || rules.LowerCaseTerms.Count == 0)
        {
            return new Dictionary<string, Regex>();
        }

        return LowerCaseTerms.BuildExpressions(
            rules.LowerCaseTerms,
            includeOrdinals: LowerCaseTerms.IsEnglish(key));
    }

    private IReadOnlyList<KnownTermReplacement> BuildKnownTermReplacements(string key)
    {
        if (!_byLanguage.TryGetValue(key, out var rules) || rules.KnownTerms.Count == 0)
        {
            return [];
        }

        return rules.KnownTerms
            .Select(term => new KnownTermReplacement(term.ToRegex(), term.Literal))
            .ToArray();
    }
}
