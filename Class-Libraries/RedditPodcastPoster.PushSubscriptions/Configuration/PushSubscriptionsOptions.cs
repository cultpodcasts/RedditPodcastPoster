namespace RedditPodcastPoster.PushSubscriptions.Configuration;

public class PushSubscriptionsOptions
{
    public required string PublicKey { get; set; }
    public required string PrivateKey { get; set; }
}