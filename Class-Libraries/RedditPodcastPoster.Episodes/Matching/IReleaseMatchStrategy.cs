namespace RedditPodcastPoster.Episodes.Matching;

public interface IReleaseMatchStrategy
{
    bool? Evaluate(ReleaseMatchContext context);
}
