namespace RedditPodcastPoster.Configuration;

public class PostingCriteria
{
    public required TimeSpan MinimumDuration { get; set; }
    public required int TweetDays { get; set; }
    public required int RedditDays { get; set; }
    public required int BlueSkyDays { get; set; }

    public int MaxDays => Math.Max(RedditDays, Math.Max(TweetDays, BlueSkyDays));

    public override string ToString()
    {
        return $"{nameof(PostingCriteria)}: minimum-duration: {MinimumDuration}, tweet-days: {TweetDays}, reddit-days: {RedditDays}, bluesky-days: {BlueSkyDays}, max-days: {MaxDays}";
    }
}
