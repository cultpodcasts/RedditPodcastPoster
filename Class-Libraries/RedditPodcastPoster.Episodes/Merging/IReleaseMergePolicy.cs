namespace RedditPodcastPoster.Episodes.Merging;

public interface IReleaseMergePolicy
{
    ReleaseMergeOpinion Evaluate(ReleaseMergeContext context);
}
