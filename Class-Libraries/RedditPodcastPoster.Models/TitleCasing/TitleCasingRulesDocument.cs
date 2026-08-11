using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.TitleCasing;

/// <summary>
/// Base document for the TitleCasingRules container (partition key <c>/language</c>).
/// </summary>
[JsonConverter(typeof(TitleCasingRulesDocumentConverter))]
[CosmosSelector(ModelType.LanguageTitleCasingRules)]
public abstract class TitleCasingRulesDocument : CosmosSelector
{
    /// <summary>Reserved partition key for known-terms that apply to every language.</summary>
    public const string UniversalLanguageKey = "*";

    protected TitleCasingRulesDocument()
    {
        ModelType = ModelType.LanguageTitleCasingRules;
    }

    protected TitleCasingRulesDocument(string language) : this()
    {
        var normalised = NormaliseLanguage(language);
        Language = normalised;
        Id = IdForLanguage(normalised);
    }

    /// <summary>ISO language code or <see cref="UniversalLanguageKey"/>; Cosmos partition key.</summary>
    [JsonPropertyName("language")]
    [JsonPropertyOrder(10)]
    public string Language { get; set; } = "";

    [JsonPropertyName("knownTerms")]
    [JsonPropertyOrder(30)]
    public List<KnownTermEntry> KnownTerms { get; set; } = [];

    public override string FileKey => IsUniversal(Language)
        ? "TitleCasingRules-universal"
        : $"TitleCasingRules-{Language}";

    public static bool IsUniversal(string? language) =>
        !string.IsNullOrWhiteSpace(language) &&
        NormaliseLanguage(language) == UniversalLanguageKey;

    public static string NormaliseLanguage(string language)
    {
        var trimmed = language.Trim();
        if (trimmed == UniversalLanguageKey)
        {
            return UniversalLanguageKey;
        }

        trimmed = trimmed.ToLowerInvariant().Replace('_', '-');
        var dash = trimmed.IndexOf('-');
        return dash > 0 ? trimmed[..dash] : trimmed;
    }

    public static Guid IdForLanguage(string language)
    {
        var normalised = NormaliseLanguage(language);
        var hash = MD5.HashData(Encoding.UTF8.GetBytes("cultpodcasts:title-casing-rules:" + normalised));
        var bytes = hash.AsSpan(0, 16).ToArray();
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x30);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    public static TitleCasingRulesDocument CreateForLanguage(string language)
    {
        var normalised = NormaliseLanguage(language);
        if (IsUniversal(normalised))
        {
            return new UniversalTitleCasingRulesDocument();
        }

        if (string.Equals(normalised, "en", StringComparison.OrdinalIgnoreCase))
        {
            return new EnglishTitleCasingRulesDocument();
        }

        return new NonEnglishTitleCasingRulesDocument(normalised);
    }

    public static EnglishTitleCasingRulesDocument CreateEnglishDefault(
        IReadOnlyList<string>? lowerCaseTerms = null,
        IReadOnlyList<KnownTermEntry>? knownTerms = null) =>
        new()
        {
            LowerCaseTerms = lowerCaseTerms?.ToList() ?? [],
            KnownTerms = knownTerms?.ToList() ?? []
        };
}
