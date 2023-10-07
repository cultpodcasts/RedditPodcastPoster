using Amazon.S3;

namespace Indexer.Data;

public interface IAmazonS3ClientFactory
{
    IAmazonS3 Create();
}