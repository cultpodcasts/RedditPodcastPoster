using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission;

/// <summary>
/// Podcast episode id to hand to search indexing after a submit.
/// A Created or Enriched podcast row contributes its id. A dry-run, a reject,
/// or a persisted Film, TV episode, or NewsReport has a null <c>Episode</c>
/// and contributes nothing, so it is not passed to <c>IndexEpisodes</c>.
/// </summary>
public static class SubmitUrlIndexableEpisodeId
{
    public static Guid? From(SubmitResult result) =>
        result.Episode is { } episode &&
        result.EpisodeResult is SubmitResultState.Created or SubmitResultState.Enriched
            ? episode.Id
            : null;
}
