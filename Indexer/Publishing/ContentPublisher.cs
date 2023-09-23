using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Indexer.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Indexer.Publishing;

public class ContentPublisher : IContentPublisher
{
    private readonly IAmazonS3 _client;
    private readonly CloudFlareOptions _options;
    private readonly ILogger<ContentPublisher> _logger;
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

    public async Task Publish()
    {
        _logger.LogInformation($"{nameof(Publish)} initiated.");
        var homepageContent = await _queryExecutor.GetHomePage(CancellationToken.None);
        var homepageContentAsJson = JsonSerializer.Serialize(homepageContent);

        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = _options.ObjectKey,
            ContentBody = homepageContentAsJson,
            ContentType = "application/json",
            DisablePayloadSigning = true
        };

        PutObjectResponse result;
        try
        {
            result = await _client.PutObjectAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload homepage-content to R2");
        }

        _logger.LogInformation($"{nameof(Publish)} complete.");
    }
}