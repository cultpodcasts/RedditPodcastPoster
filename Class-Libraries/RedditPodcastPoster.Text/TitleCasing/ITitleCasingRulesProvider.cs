using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.TitleCasing;

namespace RedditPodcastPoster.Text.TitleCasing;

public interface ITitleCasingRulesProvider
{
    IReadOnlyDictionary<string, TitleCasingRulesDocument> GetAll();

    IDictionary<string, Regex> GetLowerCaseExpressions(string? language);

    IReadOnlyList<KnownTermEntry> GetKnownTerms(string? language);

    /// <summary>Known terms that apply to every language (Cosmos language key <c>*</c>).</summary>
    IReadOnlyList<KnownTermEntry> GetUniversalKnownTerms();

    /// <summary>Precompiled language known-term replacements (empty when language is universal or unknown).</summary>
    IReadOnlyList<KnownTermReplacement> GetKnownTermReplacements(string? language);

    /// <summary>Precompiled universal (<c>*</c>) known-term replacements.</summary>
    IReadOnlyList<KnownTermReplacement> GetUniversalKnownTermReplacements();

    /// <summary>
    /// Ensures a non-English language document is in the title-casing cache (lazy Cosmos point-read).
    /// No-op for English, universal, or when already cached.
    /// </summary>
    Task EnsureLanguageLoadedAsync(string? language, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subject names ignored during enrichment for this language (from the cached title-casing doc).
    /// English / universal / missing → empty.
    /// </summary>
    Task<IReadOnlyList<string>> GetIgnoredSubjectsAsync(
        string? language,
        CancellationToken cancellationToken = default);
}
