namespace RedditPodcastPoster.Reddit;

public class SubredditSettings
{
    public string SubredditName { get; set; } = "";
    public int SubredditTitleMaxLength { get; set; }
    public bool UseDevvit { get; set; }
}