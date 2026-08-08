using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.TitleCasing;

namespace RedditPodcastPoster.Text.TitleCasing;

public interface ITitleCasingRulesProvider
{
    IReadOnlyDictionary<string, LanguageTitleCasingRulesDocument> GetAll();

    IDictionary<string, Regex> GetLowerCaseExpressions(string? language);

    IReadOnlyList<KnownTermEntry> GetKnownTerms(string? language);

    /// <summary>Known terms that apply to every language (Cosmos language key <c>*</c>).</summary>
    IReadOnlyList<KnownTermEntry> GetUniversalKnownTerms();

    /// <summary>Precompiled language known-term replacements (empty when language is universal or unknown).</summary>
    IReadOnlyList<KnownTermReplacement> GetKnownTermReplacements(string? language);

    /// <summary>Precompiled universal (<c>*</c>) known-term replacements.</summary>
    IReadOnlyList<KnownTermReplacement> GetUniversalKnownTermReplacements();
}
