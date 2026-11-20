using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Processors;

/// <summary>
/// #1416: decides what a Spotify / Apple submit does when it matches a stored episode of the podcast except that
/// the episode already carries a different id for that service. Runs before the episode is enriched or created.
/// </summary>
public interface IServiceLinkConflictGate
{
    /// <summary>Decides the outcome for <paramref name="categorisedItem"/> under the curator's answer.</summary>
    /// <remarks>
    /// Not side-effect free: on <see cref="ServiceLinkConflictOutcomeKind.Overwritten"/> the stored episode has
    /// already had its conflicting links moved onto <see cref="ServiceLinkConflictOutcome.SplitOff"/> in memory.
    /// The caller must persist both episodes, the split-off first. Every other outcome leaves the stored episode
    /// untouched. Call once per submit.
    /// </remarks>
    ServiceLinkConflictOutcome Evaluate(CategorisedItem categorisedItem, SubmitOptions submitOptions);
}
