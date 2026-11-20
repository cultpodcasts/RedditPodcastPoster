using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Processors;

public enum ServiceLinkConflictOutcomeKind
{
    /// <summary>No conflict, or the caller chose create-new: submit carries on as before #1416.</summary>
    Proceed,

    /// <summary>Nothing may be written: the curator has to answer <see cref="ServiceLinkConflictOutcome.Hold"/>.</summary>
    Hold,

    /// <summary>The curator cancelled: nothing may be written.</summary>
    Cancelled,

    /// <summary>
    /// The stored episode's conflicting links were split off into <see cref="ServiceLinkConflictOutcome.SplitOff"/>;
    /// the submit now enriches <see cref="ServiceLinkConflictOutcome.Episode"/> with the submitted links.
    /// </summary>
    /// <remarks>
    /// The stored episode has already been modified in memory (its conflicting links cleared). The caller must save
    /// <see cref="ServiceLinkConflictOutcome.SplitOff"/> before the stored episode, so a failed second save leaves the
    /// old links stored twice rather than lost.
    /// </remarks>
    Overwritten
}

public sealed record ServiceLinkConflictOutcome(
    ServiceLinkConflictOutcomeKind Kind,
    ServiceLinkConflictHold? Hold = null,
    Episode? Episode = null,
    Episode? SplitOff = null)
{
    public static readonly ServiceLinkConflictOutcome Proceed = new(ServiceLinkConflictOutcomeKind.Proceed);

    public static readonly ServiceLinkConflictOutcome Cancelled = new(ServiceLinkConflictOutcomeKind.Cancelled);

    public static ServiceLinkConflictOutcome Held(ServiceLinkConflictHold hold) =>
        new(ServiceLinkConflictOutcomeKind.Hold, hold);

    public static ServiceLinkConflictOutcome Overwritten(Episode episode, Episode splitOff) =>
        new(ServiceLinkConflictOutcomeKind.Overwritten, Episode: episode, SplitOff: splitOff);
}
