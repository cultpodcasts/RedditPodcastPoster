using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.ContentPublisher.Configuration;

namespace RedditPodcastPoster.ContentPublisher;

public class ContentPublisher(
    IQueryExecutor queryExecutor,
    IAmazonS3 client,
    IOptions<CloudFlareOptions> options,
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

        PutObjectResponse result;
        try
        {
            result = await client.PutObjectAsync(request);
            logger.LogInformation($"Completed '{nameof(PublishHomepage)}'.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(PublishHomepage)} - Failed to upload homepage-content to R2");
        }
    }
}