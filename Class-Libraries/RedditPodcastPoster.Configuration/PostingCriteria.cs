namespace RedditPodcastPoster.Configuration;

public class PostingCriteria
{
    public required TimeSpan MinimumDuration { get; set; }
    public required int TweetDays { get; set; }

    public override string ToString()
    {
        return $"{nameof(PostingCriteria)}: minimum-duration: {MinimumDuration.ToString()}";
    }
}