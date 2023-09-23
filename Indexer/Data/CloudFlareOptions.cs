namespace Indexer.Data;

public class CloudFlareOptions
{
    public string AccountId { get; set; }
    public string R2AccessKey { get; set; }
    public string R2SecretKey { get; set; }
    public string BucketName { get; set; }
    public string ObjectKey { get; set; }
}