namespace RedditPodcastPoster.Configuration;

public class CloudFlareOptions
{
    public required string AccountId { get; set; }
    public required string R2AccessKey { get; set; }
    public required string R2SecretKey { get; set; }
    public required string BucketName { get; set; }
    public required string HomepageKey { get; set; }
    public required string SubjectsKey { get; set; }
    public required string KVApiToken { get; set; }
    public required string KVShortnerNamespaceId { get; set; }
    public required string FlairsKey { get; set; }
}