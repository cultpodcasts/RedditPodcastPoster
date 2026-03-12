using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.ContentPublisher.Models;

namespace RedditPodcastPoster.ContentPublisher;

public class DiscoveryPublisher(
    IAmazonS3 client,
    IOptions<ContentOptions> contentOptions,
    ILogger<DiscoveryPublisher> logger) : IDiscoveryPublisher
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ContentOptions _contentOptions = contentOptions.Value;

    public async Task PublishDiscoveryInfo(DiscoveryInfo discoveryInfo)
    {
        var json = JsonSerializer.Serialize(discoveryInfo, JsonSerializerOptions);
        var request = new PutObjectRequest
        {
            BucketName = _contentOptions.BucketName,
            Key = _contentOptions.DiscoveryInfoKey,
            ContentBody = json,
            ContentType = "application/json",
            DisablePayloadSigning = true
        };

        try
        {
            await client.PutObjectAsync(request);
            logger.LogInformation("Completed '{MethodName}'.", nameof(PublishDiscoveryInfo));
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{MethodName} - Failed to upload discovery-info-content to R2. BucketName: '{BucketName}', Key: '{Key}', content: '{Json}'.",
                nameof(PublishDiscoveryInfo), _contentOptions.BucketName, _contentOptions.DiscoveryInfoKey, json);
        }
    }
}
