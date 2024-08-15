namespace RedditPodcastPoster.Reddit;

public class AdminRedditSettings
{
    public required string AppId { get; set; }
    public required string AppSecret { get; set; }
    public required string RefreshToken { get; set; }
}