using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.ContentPublisher.Models;

namespace RedditPodcastPoster.ContentPublisher;

public class DiscoveryInfoRepository(
    IAmazonS3 client,
    IOptions<ContentOptions> contentOptions,
    ILogger<DiscoveryInfoRepository> logger) : IDiscoveryInfoRepository
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ContentOptions _contentOptions = contentOptions.Value;

    public async Task<DiscoveryInfo?> Get(CancellationToken cancellationToken = default)
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
                nameof(Get), _contentOptions.BucketName, _contentOptions.DiscoveryInfoKey);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{MethodName} - Failed to read discovery-info from R2. BucketName: '{BucketName}', Key: '{Key}'.",
                nameof(Get), _contentOptions.BucketName, _contentOptions.DiscoveryInfoKey);
            throw;
        }
    }
}
