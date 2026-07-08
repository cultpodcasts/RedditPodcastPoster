namespace RedditPodcastPoster.UrlSubmission.Models;

/// <summary>
/// Outcome of applying a resolved platform candidate to an existing episode during URL submission.
/// </summary>
public sealed record ResolvedPlatformApplyOutcome(
    bool PlatformLinkAdded,
    bool EpisodeEnriched)
{
    public static ResolvedPlatformApplyOutcome None { get; } = new(false, false);
}
