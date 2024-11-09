namespace RedditPodcastPoster.Twitter;

public class TwitterOptions
{
    public required string ConsumerKey { get; set; }
    public required string ConsumerSecret { get; set; }
    public required string AccessToken { get; set; }
    public required string AccessTokenSecret { get; set; }
    public string? HashTag { get; set; }
    public bool WithEpisodeUrl { get; set; }
    public long TwitterId { get; set; }
}