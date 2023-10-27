using Amazon.S3;

namespace RedditPodcastPoster.ContentPublisher.Factories;

public interface IAmazonS3ClientFactory
{
    IAmazonS3 Create();
}