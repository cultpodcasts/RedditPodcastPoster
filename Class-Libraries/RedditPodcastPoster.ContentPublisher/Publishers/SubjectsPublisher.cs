using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Amazon.S3;
using Amazon.S3.Model;
using Flair = RedditPodcastPoster.ContentPublisher.Models.Flair;
using RedditPodcastPoster.ContentPublisher.Configuration;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.ContentPublisher.Publishers;

public class SubjectsPublisher(
    IAmazonS3 client,
    IOptions<ContentOptions> contentOptions,
    ISubjectRepository subjectRepository,
    ILogger<SubjectsPublisher> logger) : ISubjectsPublisher
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ContentOptions _contentOptions = contentOptions.Value;

    public async Task PublishSubjects()
    {
        var subjects = await subjectRepository.GetAll()
            .Select(x => new { name = x.Name })
            .OrderBy(x => x.name)
            .ToListAsync();

        var request = new PutObjectRequest
        {
            BucketName = _contentOptions.BucketName,
            Key = _contentOptions.SubjectsKey,
            ContentBody = JsonSerializer.Serialize(subjects, JsonSerializerOptions),
            ContentType = "application/json",
            DisablePayloadSigning = true
        };

        try
        {
            await client.PutObjectAsync(request);
            logger.LogInformation("Completed '{MethodName}'.", nameof(PublishSubjects));
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{MethodName} - Failed to upload subject-content to R2. BucketName: '{BucketName}', Key: '{Key}'.",
                nameof(PublishSubjects), _contentOptions.BucketName, _contentOptions.SubjectsKey);
        }
    }

    public async Task PublishFlairs()
    {
        // Live Reddit flair sync retired with Reddit.NET. Rebuild R2 from Cosmos subject flair fields.
        var subjects = await subjectRepository.GetAll().ToListAsync();
            var models = subjects
            .Where(x => x.RedditFlairTemplateId.HasValue)
            .GroupBy(x => x.RedditFlairTemplateId!.Value)
            .ToDictionary(
                g => g.Key.ToString(),
                g => new Flair
                {
                    Text = g.Select(s => s.RedditFlareText).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t))
                           ?? g.First().Name,
                    TextEditable = true,
                    TextColour = "dark",
                    BackgroundColour = "#dadada"
                });

        var request = new PutObjectRequest
        {
            BucketName = _contentOptions.BucketName,
            Key = _contentOptions.FlairsKey,
            ContentBody = JsonSerializer.Serialize(models, JsonSerializerOptions),
            ContentType = "application/json",
            DisablePayloadSigning = true
        };

        try
        {
            await client.PutObjectAsync(request);
            logger.LogInformation(
                "Completed '{MethodName}' from Cosmos subject flair fields ({Count} templates).",
                nameof(PublishFlairs),
                models.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{MethodName} - Failed to upload flairs-content to R2. BucketName: '{BucketName}', Key: '{Key}'.",
                nameof(PublishFlairs), _contentOptions.BucketName, _contentOptions.FlairsKey);
        }
    }
}
