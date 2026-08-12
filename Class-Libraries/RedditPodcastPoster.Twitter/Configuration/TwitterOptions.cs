namespace RedditPodcastPoster.Twitter.Configuration;

public class TwitterOptions
{
    public required string ConsumerKey { get; set; }
    public required string ConsumerSecret { get; set; }
    public required string AccessToken { get; set; }
    public required string AccessTokenSecret { get; set; }
    public string? HashTag { get; set; }
    public bool WithEpisodeUrl { get; set; }
    /// <summary>
    /// When true and shortener KV has a share image, posts use only the short URL
    /// (no platform link). Default false — ship dark until SEO/social rollout is ready.
    /// </summary>
    public bool ShortUrlOnlyWhenShareImage { get; set; }
    public long TwitterId { get; set; }
}
