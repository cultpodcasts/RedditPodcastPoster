using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RedditPodcastPoster.Cloudflare.Factories;

public class AmazonS3ClientFactory(
    IOptions<CloudFlareOptions> r2Options,
    ILogger<AmazonS3ClientFactory> logger)
    : IAmazonS3ClientFactory
{
    private readonly CloudFlareOptions _cloudFlareOptions = r2Options.Value;

    public IAmazonS3 Create()
    {
        var missingAccountId = string.IsNullOrWhiteSpace(_cloudFlareOptions.AccountId);
        var missingAccessKey = string.IsNullOrWhiteSpace(_cloudFlareOptions.R2AccessKey);
        var missingSecretKey = string.IsNullOrWhiteSpace(_cloudFlareOptions.R2SecretKey);
        if (missingAccountId || missingAccessKey || missingSecretKey)
        {
            logger.LogError(
                "Incomplete {optionsName} configuration. missingAccountId={missingAccountId}, missingAccessKey={missingAccessKey}, missingSecretKey= {missingSecretKey}.",
                nameof(CloudFlareOptions), missingAccountId, missingAccessKey, missingSecretKey);
            throw new InvalidOperationException(
                $"Incomplete {nameof(CloudFlareOptions)} configuration. missingAccountId={missingAccountId}, missingAccessKey={missingAccessKey}, missingSecretKey= {missingSecretKey}.");
        }

        var config = new AmazonS3Config
        {
            ServiceURL = $"https://{_cloudFlareOptions.AccountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true
        };

        AWSCredentials credentials =
            new BasicAWSCredentials(_cloudFlareOptions.R2AccessKey, _cloudFlareOptions.R2SecretKey);
        var s3Client = new AmazonS3Client(credentials, config);

        return s3Client;
    }
}