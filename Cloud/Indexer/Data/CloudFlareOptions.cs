namespace Indexer.Data;

public class CloudFlareOptions
{
    public required string AccountId { get; set; }
    public required string R2AccessKey { get; set; }
    public required string R2SecretKey { get; set; }
    public required string BucketName { get; set; }
    public required string ObjectKey { get; set; }
}