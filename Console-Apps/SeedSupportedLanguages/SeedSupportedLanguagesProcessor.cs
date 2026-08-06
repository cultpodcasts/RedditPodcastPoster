using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.ContentPublisher.Configuration;
using RedditPodcastPoster.Models.Languages;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace SeedSupportedLanguages;

public class SeedSupportedLanguagesProcessor(
    ILookupRepository lookupRepository,
    IAmazonS3 s3,
    IOptions<ContentOptions> contentOptions,
    ILogger<SeedSupportedLanguagesProcessor> logger)
{
    public async Task<int> Run(SeedSupportedLanguagesRequest request)
    {
        var existing = await lookupRepository.GetSupportedLanguagesConfig();
        SupportedLanguagesConfig document;

        if (request.FromR2)
        {
            document = await BuildFromR2Async();
            logger.LogInformation(
                "Built SupportedLanguagesConfig from R2 ({Count} languages).",
                document.Languages.Count);
        }
        else
        {
            document = SupportedLanguagesConfig.CreateDefault();
            logger.LogInformation(
                "Built SupportedLanguagesConfig from CreateDefault ({Count} languages).",
                document.Languages.Count);
        }

        logger.LogInformation(
            "Would seed SupportedLanguagesConfig id={Id}: {Count} languages. Existing={Exists}. Apply={Apply}. Force={Force}. FromR2={FromR2}.",
            document.Id,
            document.Languages.Count,
            existing is not null,
            request.Apply,
            request.Force,
            request.FromR2);

        if (existing is not null && !request.Force)
        {
            logger.LogInformation(
                "SupportedLanguagesConfig already exists with {Count} languages — skipping write (pass --force to overwrite).",
                existing.Languages.Count);
            return 0;
        }

        if (!request.Apply)
        {
            logger.LogInformation("Dry-run only. Pass --apply to write.");
            return 0;
        }

        await lookupRepository.SaveSupportedLanguagesConfig(document);
        logger.LogInformation("Saved SupportedLanguagesConfig with {Count} languages.", document.Languages.Count);
        return 0;
    }

    private async Task<SupportedLanguagesConfig> BuildFromR2Async()
    {
        var content = contentOptions.Value;
        var response = await s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = content.BucketName,
            Key = content.LanguagesKey
        });
        await using var stream = response.ResponseStream;
        var map = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream)
                  ?? throw new InvalidOperationException("R2 languages object was empty or invalid JSON.");

        var languages = map
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => new SupportedLanguage
            {
                Code = kv.Key.Trim(),
                Name = kv.Value.Trim()
            })
            .DistinctBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (languages.Count == 0)
        {
            throw new InvalidOperationException("R2 languages object contained no usable entries.");
        }

        return new SupportedLanguagesConfig { Languages = languages };
    }
}
