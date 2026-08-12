namespace RedditPodcastPoster.Bluesky.Configuration;

public class BlueskyOptions
{
    public required string Identifier { get; set; }
    public required string Password { get; set; }
    public string? HashTag { get; set; }
    public bool WithEpisodeUrl { get; set; }
    /// <summary>
    /// When true and shortener KV has a share image, posts use only the short URL
    /// as the embed card URL. Default false — ship dark until SEO/social rollout is ready.
    /// </summary>
    public bool ShortUrlOnlyWhenShareImage { get; set; }
    public required bool ReuseSession { get; set; }
    public required int MaxFailures { get; set; }
    public required int MaxPosts { get; set; }
}