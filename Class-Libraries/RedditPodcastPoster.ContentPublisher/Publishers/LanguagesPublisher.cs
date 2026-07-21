using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.ContentPublisher.Configuration;

namespace RedditPodcastPoster.ContentPublisher.Publishers;

public class LanguagesPublisher(
    IAmazonS3 client,
    IOptions<ContentOptions> contentOptions,
    ILogger<LanguagesPublisher> logger) : ILanguagesPublisher
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string[] LanguageNames =
    [
        "English", "French", "Spanish", "German", "Portuguese", "Turkish", "Dutch", "Italian", "Japanese", "Chinese", "Korean",
        "Hindi", "Russian", "Hebrew", "Arabic", "Bangla", "Indonesian", "Filipino", "Urdu", "Kiswahili",
        "Vietnamese", "Slovak", "Czech", "Telugu", "Afrikaans", "Persian", "Malay", "Norwegian", "Polish", "Punjabi", "Thai",
        "Ukrainian", "Marathi", "Finnish", "Danish", "Greek", "Hungarian", "Swedish", "Bulgarian", "Serbian", "Croatian", "Lithuanian", "Latvian", "Slovenian", "Bosnian", "Macedonian", "Albanian", "Estonian", "Catalan", "Sinhala", "Yiddish"
    ];

    private readonly ContentOptions _contentOptions = contentOptions.Value;

    public async Task<bool> PublishLanguages()
    {
        try
        {
            var languages = ResolveLanguages();
            var request = new PutObjectRequest
            {
                BucketName = _contentOptions.BucketName,
                Key = _contentOptions.LanguagesKey,
                ContentBody = JsonSerializer.Serialize(
                    languages.DistinctBy(x => x.TwoLetterISOLanguageName)
                        .OrderBy(x => x.EnglishName)
                        .ToDictionary(x => x.TwoLetterISOLanguageName, x => x.EnglishName),
                    JsonSerializerOptions),
                ContentType = "application/json",
                DisablePayloadSigning = true
            };

            await client.PutObjectAsync(request);
            logger.LogInformation("Completed '{MethodName}'. Published {LanguageCount} languages to '{Key}'.",
                nameof(PublishLanguages), languages.Count, _contentOptions.LanguagesKey);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{MethodName} - Failed to publish languages-content to R2. BucketName: '{BucketName}', Key: '{Key}'.",
                nameof(PublishLanguages), _contentOptions.BucketName, _contentOptions.LanguagesKey);
            return false;
        }
    }

    private static List<CultureInfo> ResolveLanguages()
    {
        var neutralCultures = CultureInfo.GetCultures(CultureTypes.NeutralCultures);
        var culturesByName = neutralCultures
            .GroupBy(culture => culture.EnglishName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var resolved = new List<CultureInfo>();
        var missing = new List<string>();

        foreach (var languageName in LanguageNames)
        {
            if (culturesByName.TryGetValue(languageName, out var culture))
            {
                resolved.Add(culture);
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

        return resolved;
    }
}
