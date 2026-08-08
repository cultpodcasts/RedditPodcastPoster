using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.Languages;

/// <summary>
/// Cosmos LookUps singleton listing languages offered in admin/UI and published to R2.
/// </summary>
[CosmosSelector(ModelType.SupportedLanguagesConfig)]
public sealed class SupportedLanguagesConfig : CosmosSelector
{
    public static readonly Guid _Id = Guid.Parse("B2C3D4E5-F6A7-8901-BCDE-F12345678901");

    /// <summary>
    /// English display names used historically by <c>LanguagesPublisher</c> (resolved to ISO codes).
    /// </summary>
    public static readonly string[] DefaultLanguageNames =
    [
        "English", "French", "Spanish", "German", "Portuguese", "Turkish", "Dutch", "Italian", "Japanese", "Chinese",
        "Korean",
        "Hindi", "Russian", "Hebrew", "Arabic", "Bangla", "Indonesian", "Filipino", "Urdu", "Kiswahili",
        "Vietnamese", "Slovak", "Czech", "Telugu", "Afrikaans", "Persian", "Malay", "Norwegian", "Polish", "Punjabi",
        "Thai",
        "Ukrainian", "Marathi", "Finnish", "Danish", "Greek", "Hungarian", "Swedish", "Bulgarian", "Serbian",
        "Croatian", "Lithuanian", "Latvian", "Slovenian", "Bosnian", "Macedonian", "Albanian", "Estonian", "Catalan",
        "Sinhala", "Yiddish"
    ];

    public SupportedLanguagesConfig()
    {
        Id = _Id;
        ModelType = ModelType.SupportedLanguagesConfig;
    }

    [JsonPropertyName("languages")]
    [JsonPropertyOrder(10)]
    public List<SupportedLanguage> Languages { get; set; } = [];

    public override string FileKey => nameof(SupportedLanguagesConfig);

    public static SupportedLanguagesConfig CreateDefault() => new()
    {
        Languages = ResolveDefaultLanguages()
    };

    public static List<SupportedLanguage> ResolveDefaultLanguages()
    {
        var resolved = new List<SupportedLanguage>();
        var missing = new List<string>();

        foreach (var languageName in DefaultLanguageNames)
        {
            if (NeutralCultureLanguageLookup.TryResolveByEnglishName(languageName, out var code, out var canonicalName))
            {
                resolved.Add(new SupportedLanguage
                {
                    Code = code,
                    Name = canonicalName
                });
            }
            else
            {
                missing.Add(languageName);
            }
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Unable to resolve neutral cultures for: {string.Join(", ", missing)}");
        }

        return resolved
            .DistinctBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
