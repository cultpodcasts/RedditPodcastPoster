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
    Episode? SplitOff = null,
    bool SaveSplitOff = true)
{
    public static readonly ServiceLinkConflictOutcome Proceed = new(ServiceLinkConflictOutcomeKind.Proceed);

    public static readonly ServiceLinkConflictOutcome Cancelled = new(ServiceLinkConflictOutcomeKind.Cancelled);

    public static ServiceLinkConflictOutcome Held(ServiceLinkConflictHold hold) =>
        new(ServiceLinkConflictOutcomeKind.Hold, hold);

    /// <param name="saveSplitOff">False when <paramref name="splitOff"/> is an existing split-off that gained no link
    /// (#1482): it is not saved again, so curator edits to it are kept.</param>
    public static ServiceLinkConflictOutcome Overwritten(Episode episode, Episode splitOff, bool saveSplitOff = true) =>
        new(ServiceLinkConflictOutcomeKind.Overwritten, Episode: episode, SplitOff: splitOff, SaveSplitOff: saveSplitOff);
}
