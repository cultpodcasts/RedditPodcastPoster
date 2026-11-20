using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Processors;

/// <summary>
/// #1416: decides what a Spotify / Apple submit does when it matches a stored episode of the podcast except that
/// the episode already carries a different id for that service. Runs before the episode is enriched or created.
/// </summary>
public interface IServiceLinkConflictGate
{
    ServiceLinkConflictOutcome Evaluate(CategorisedItem categorisedItem, SubmitOptions submitOptions);
}
