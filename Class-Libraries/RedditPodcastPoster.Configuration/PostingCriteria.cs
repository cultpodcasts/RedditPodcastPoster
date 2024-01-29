namespace RedditPodcastPoster.Configuration;

public class PostingCriteria
{
    public TimeSpan MinimumDuration { get; set; }

    public override string ToString()
    {
        return $"{nameof(PostingCriteria)}: minimum-duration: {MinimumDuration.ToString()}";
    }
}