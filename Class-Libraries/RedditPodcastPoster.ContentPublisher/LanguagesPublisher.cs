using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RedditPodcastPoster.ContentPublisher;

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

    private readonly ContentOptions _contentOptions = contentOptions.Value;

    public async Task PublishLanguages()
    {
        var allCultures = CultureInfo.GetCultures(CultureTypes.AllCultures);
        var languages = new[]
        {
            "French", "Spanish", "German", "Portuguese", "Turkish", "Dutch", "Italian", "Japanese", "Chinese", "Korean",
            "Hindi", "Russian", "Hebrew", "Arabic", "Bangla", "Indonesian", "Filipino", "Urdu", "Kiswahili",
            "Vietnamese", "Slovak", "Czech", "Telugu", "Afrikaans", "Persian", "Malay", "Norwegian", "Punjabi", "Thai",
            "Ukrainian", "Marathi", "Finnish", "Danish", "Greek", "Hungarian", "Swedish", "Bulgarian", "Serbian", "Croatian", "Lithuanian", "Latvian", "Slovenian", "Bosnian", "Macedonian", "Albanian", "Estonian", "Catalan", "Sinhala"
        }.Select(x => allCultures.Single(y => y.IsNeutralCulture && y.EnglishName == x));

        var request = new PutObjectRequest
        {
            BucketName = _contentOptions.BucketName,
            Key = _contentOptions.LanguagesKey,
            ContentBody = JsonSerializer.Serialize(
                languages.Distinct().OrderBy(x => x.EnglishName)
                    .ToDictionary(x => x.TwoLetterISOLanguageName, x => x.EnglishName),
                JsonSerializerOptions),
            ContentType = "application/json",
            DisablePayloadSigning = true
        };

        try
        {
            await client.PutObjectAsync(request);
            logger.LogInformation("Completed '{MethodName}'.", nameof(PublishLanguages));
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{MethodName} - Failed to upload languages-content to R2. BucketName: '{BucketName}', Key: '{Key}'.",
                nameof(PublishLanguages), _contentOptions.BucketName, _contentOptions.LanguagesKey);
        }
    }
}
