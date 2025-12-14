namespace RedditPodcastPoster.Bluesky.Configuration;

public class BlueskyOptions
{
    public required string Identifier { get; set; }
    public required string Password { get; set; }
    public string? HashTag { get; set; }
    public bool WithEpisodeUrl { get; set; }
    public required bool ReuseSession { get; set; }
}