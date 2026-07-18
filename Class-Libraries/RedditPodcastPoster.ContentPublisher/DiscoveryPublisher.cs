using System.Net;
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

    public async Task<DiscoveryInfo?> GetDiscoveryInfo(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _contentOptions.BucketName,
                Key = _contentOptions.DiscoveryInfoKey
            }, cancellationToken);

            await using var stream = response.ResponseStream;
            return await JsonSerializer.DeserializeAsync<DiscoveryInfo>(stream, JsonSerializerOptions, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogInformation(
                "{MethodName}: no discovery-info at BucketName: '{BucketName}', Key: '{Key}'.",
                nameof(GetDiscoveryInfo), _contentOptions.BucketName, _contentOptions.DiscoveryInfoKey);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{MethodName} - Failed to read discovery-info from R2. BucketName: '{BucketName}', Key: '{Key}'.",
                nameof(GetDiscoveryInfo), _contentOptions.BucketName, _contentOptions.DiscoveryInfoKey);
            throw;
        }
    }
}
