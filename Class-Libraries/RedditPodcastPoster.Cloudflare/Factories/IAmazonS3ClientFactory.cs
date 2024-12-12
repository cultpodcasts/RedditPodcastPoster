using Amazon.S3;

namespace RedditPodcastPoster.Cloudflare.Factories;

public interface IAmazonS3ClientFactory
{
    IAmazonS3 Create();
}