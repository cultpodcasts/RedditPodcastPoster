namespace RedditPodcastPoster.Configuration;

public class DelayedYouTubePublication
{
    public TimeSpan EvaluationThreshold { get; set; }

    public override string ToString()
    {
        return $"{nameof(DelayedYouTubePublication)}: {{Evaluation-Threshold: {EvaluationThreshold.ToString()}}}";
    }

}