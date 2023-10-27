using Amazon.S3;

namespace RedditPodcastPoster.ContentPublisher;

public interface IAmazonS3ClientFactory
{
    IAmazonS3 Create();
}