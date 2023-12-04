using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.ContentPublisher.Configuration;

namespace RedditPodcastPoster.ContentPublisher;

public class ContentPublisher : IContentPublisher
{
    private readonly IAmazonS3 _client;
    private readonly ILogger<ContentPublisher> _logger;
    private readonly CloudFlareOptions _options;
    private readonly IQueryExecutor _queryExecutor;

    public ContentPublisher(
        IQueryExecutor queryExecutor,
        IAmazonS3 client,
        IOptions<CloudFlareOptions> options,
        ILogger<ContentPublisher> logger)
    {
        _queryExecutor = queryExecutor;
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishHomepage()
    {
        var homepageContent = await _queryExecutor.GetHomePage(CancellationToken.None);
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
            result = await _client.PutObjectAsync(request);
            _logger.LogInformation($"Completed '{nameof(PublishHomepage)}'.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{nameof(PublishHomepage)} - Failed to upload homepage-content to R2");
        }
    }
}