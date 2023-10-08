namespace Indexer.Tweets;

public class TwitterOptions
{
    public required string ConsumerKey { get; set; }
    public required string ConsumerSecret { get; set; }
    public required string AccessToken { get; set; }
    public required string AccessTokenSecret { get; set; }
}