using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.ContentPublisher;

public class ContentPublisher(
    IQueryExecutor queryExecutor,
    IAmazonS3 client,
    IOptions<CloudFlareOptions> options,
    ISubjectRepository subjectRepository,
    ILogger<ContentPublisher> logger)
    : IContentPublisher
{
    private readonly CloudFlareOptions _options = options.Value;

    public async Task PublishHomepage()
    {
        var homepageContent = await queryExecutor.GetHomePage(CancellationToken.None);
        var homepageContentAsJson = JsonSerializer.Serialize(homepageContent);

        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = _options.HomepageKey,
            ContentBody = homepageContentAsJson,
            ContentType = "application/json",
            DisablePayloadSigning = true
        };

        try
        {
            await client.PutObjectAsync(request);
            logger.LogInformation($"Completed '{nameof(PublishHomepage)}'.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(PublishHomepage)} - Failed to upload homepage-content to R2");
        }
    }

    public async Task PublishSubjects()
    {
        var subjects = await subjectRepository.GetAll(x => new {name = x.Name}).ToListAsync();
        var json = JsonSerializer.Serialize(subjects);

        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = _options.SubjectsKey,
            ContentBody = json,
            ContentType = "application/json",
            DisablePayloadSigning = true
        };

        try
        {
            await client.PutObjectAsync(request);
            logger.LogInformation($"Completed '{nameof(PublishSubjects)}'.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(PublishSubjects)} - Failed to upload subjects-content to R2");
        }
    }
}