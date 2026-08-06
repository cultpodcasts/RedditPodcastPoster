using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.ContentPublisher.Configuration;
using RedditPodcastPoster.Models.Languages;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.ContentPublisher.Publishers;

public class LanguagesPublisher(
    IAmazonS3 client,
    ILookupRepository lookupRepository,
    IOptions<ContentOptions> contentOptions,
    ILogger<LanguagesPublisher> logger) : ILanguagesPublisher
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ContentOptions _contentOptions = contentOptions.Value;

    public async Task<bool> PublishLanguages()
    {
        try
        {
            var config = await lookupRepository.GetSupportedLanguagesConfig()
                         ?? SupportedLanguagesConfig.CreateDefault();
            var map = config.Languages
                .Where(x => !string.IsNullOrWhiteSpace(x.Code) && !string.IsNullOrWhiteSpace(x.Name))
                .DistinctBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Code, x => x.Name, StringComparer.OrdinalIgnoreCase);

            var request = new PutObjectRequest
            {
                BucketName = _contentOptions.BucketName,
                Key = _contentOptions.LanguagesKey,
                ContentBody = JsonSerializer.Serialize(map, JsonSerializerOptions),
                ContentType = "application/json",
                DisablePayloadSigning = true
            };

            await client.PutObjectAsync(request);
            logger.LogInformation("Completed '{MethodName}'. Published {LanguageCount} languages to '{Key}'.",
                nameof(PublishLanguages), map.Count, _contentOptions.LanguagesKey);
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
}
