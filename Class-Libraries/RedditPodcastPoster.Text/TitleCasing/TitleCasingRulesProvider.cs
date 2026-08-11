using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Text.Models;

namespace RedditPodcastPoster.Text.TitleCasing;

public class TitleCasingRulesProvider : ITitleCasingRulesProvider
{
    private readonly ConcurrentDictionary<string, TitleCasingRulesDocument> _byLanguage;
    private readonly Func<string, CancellationToken, Task<TitleCasingRulesDocument?>>? _loadLanguage;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _loadGates =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _missingLanguages =
        new(StringComparer.OrdinalIgnoreCase);

    // Homepage (and other) sanitise runs titles in parallel; caches must be concurrency-safe.
    private readonly ConcurrentDictionary<string, IDictionary<string, Regex>> _lowerCache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, IReadOnlyList<KnownTermReplacement>> _knownTermCache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyList<string> EmptyIgnoredSubjects = [];
    private static readonly IDictionary<string, Regex> EmptyLowerCase = new Dictionary<string, Regex>();

    public TitleCasingRulesProvider(
        IReadOnlyDictionary<string, TitleCasingRulesDocument> byLanguage,
        Func<string, CancellationToken, Task<TitleCasingRulesDocument?>>? loadLanguage = null)
    {
        _byLanguage = new ConcurrentDictionary<string, TitleCasingRulesDocument>(
            byLanguage,
            StringComparer.OrdinalIgnoreCase);
        _loadLanguage = loadLanguage;
        // Eager compile for English + universal — the common homepage path.
        _ = GetLowerCaseExpressions("en");
        _ = GetUniversalKnownTermReplacements();
        _ = GetKnownTermReplacements("en");
    }

    public IReadOnlyDictionary<string, TitleCasingRulesDocument> GetAll() => _byLanguage;

    public IDictionary<string, Regex> GetLowerCaseExpressions(string? language)
    {
        var key = LowerCaseTerms.NormaliseLanguageKey(language);
        // Do not cache empty results for languages that may still be lazy-loaded.
        if (!_byLanguage.ContainsKey(key))
        {
            return EmptyLowerCase;
        }

        var cached = _lowerCache.GetOrAdd(key, BuildLowerCaseExpressions);
        return RefreshStaleLowerCache(key, cached);
    }

    public IReadOnlyList<KnownTermEntry> GetKnownTerms(string? language)
    {
        var key = LowerCaseTerms.NormaliseLanguageKey(language);
        if (key == TitleCasingRulesDocument.UniversalLanguageKey ||
            !_byLanguage.TryGetValue(key, out var rules))
        {
            return [];
        }

        return rules.KnownTerms;
    }

    public IReadOnlyList<KnownTermEntry> GetUniversalKnownTerms()
    {
        if (!_byLanguage.TryGetValue(TitleCasingRulesDocument.UniversalLanguageKey, out var rules))
        {
            return [];
        }

        return rules.KnownTerms;
    }

    public IReadOnlyList<KnownTermReplacement> GetKnownTermReplacements(string? language)
    {
        var key = LowerCaseTerms.NormaliseLanguageKey(language);
        if (key == TitleCasingRulesDocument.UniversalLanguageKey)
        {
            return [];
        }

        if (!_byLanguage.ContainsKey(key))
        {
            return [];
        }

        var cached = _knownTermCache.GetOrAdd(key, BuildKnownTermReplacements);
        return RefreshStaleKnownTermCache(key, cached);
    }

    public IReadOnlyList<KnownTermReplacement> GetUniversalKnownTermReplacements() =>
        _knownTermCache.GetOrAdd(
            TitleCasingRulesDocument.UniversalLanguageKey,
            BuildKnownTermReplacements);

    public async Task EnsureLanguageLoadedAsync(
        string? language,
        CancellationToken cancellationToken = default)
    {
        var key = LowerCaseTerms.NormaliseLanguageKey(language);
        if (key == TitleCasingRulesDocument.UniversalLanguageKey ||
            LowerCaseTerms.IsEnglish(key) ||
            _byLanguage.ContainsKey(key) ||
            _missingLanguages.ContainsKey(key) ||
            _loadLanguage is null)
        {
            return;
        }

        var gate = _loadGates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (_byLanguage.ContainsKey(key) || _missingLanguages.ContainsKey(key))
            {
                return;
            }

            var document = await _loadLanguage(key, cancellationToken);
            if (document is null)
            {
                _missingLanguages[key] = 0;
                return;
            }

            _byLanguage[key] = document;
            // Invalidate compiled caches so any empty race loser is discarded.
            _lowerCache.TryRemove(key, out _);
            _knownTermCache.TryRemove(key, out _);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<string>> GetIgnoredSubjectsAsync(
        string? language,
        CancellationToken cancellationToken = default)
    {
        var key = LowerCaseTerms.NormaliseLanguageKey(language);
        if (key == TitleCasingRulesDocument.UniversalLanguageKey ||
            LowerCaseTerms.IsEnglish(key))
        {
            return EmptyIgnoredSubjects;
        }

        await EnsureLanguageLoadedAsync(key, cancellationToken);
        if (!_byLanguage.TryGetValue(key, out var rules) ||
            rules is not NonEnglishTitleCasingRulesDocument nonEnglish ||
            nonEnglish.IgnoredSubjects is null ||
            nonEnglish.IgnoredSubjects.Length == 0)
        {
            return EmptyIgnoredSubjects;
        }

        return nonEnglish.IgnoredSubjects;
    }

    private IDictionary<string, Regex> RefreshStaleLowerCache(
        string key,
        IDictionary<string, Regex> cached)
    {
        if (cached.Count > 0 ||
            !_byLanguage.TryGetValue(key, out var rules) ||
            rules is not LanguageTitleCasingRulesDocument languageRules ||
            languageRules.LowerCaseTerms.Count == 0)
        {
            return cached;
        }

        var rebuilt = BuildLowerCaseExpressions(key);
        _lowerCache[key] = rebuilt;
        return rebuilt;
    }

    private IReadOnlyList<KnownTermReplacement> RefreshStaleKnownTermCache(
        string key,
        IReadOnlyList<KnownTermReplacement> cached)
    {
        if (cached.Count > 0 ||
            !_byLanguage.TryGetValue(key, out var rules) ||
            rules.KnownTerms.Count == 0)
        {
            return cached;
        }

        var rebuilt = BuildKnownTermReplacements(key);
        _knownTermCache[key] = rebuilt;
        return rebuilt;
    }

    private IDictionary<string, Regex> BuildLowerCaseExpressions(string key)
    {
        if (!_byLanguage.TryGetValue(key, out var rules) ||
            rules is not LanguageTitleCasingRulesDocument languageRules ||
            languageRules.LowerCaseTerms.Count == 0)
        {
            return new Dictionary<string, Regex>();
        }

        return LowerCaseTerms.BuildExpressions(
            languageRules.LowerCaseTerms,
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
