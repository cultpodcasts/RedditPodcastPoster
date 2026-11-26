namespace RedditPodcastPoster.UrlSubmission.Categorisation;

/// <summary>
/// ADR-0004 CLS-002 (#1521): a Netflix <c>/title/</c> page for a series is a catalogue hub.
/// Submit rejects it (no TvShow stub) and tells the submitter to paste an episode or watch URL.
/// A <c>/title/</c> page whose primary type is Movie stays a Film (#1517).
/// </summary>
public static class NetflixSeriesHubSubmitRejection
{
    public const string Message =
        "This is a Netflix series page. Submit an episode/watch URL instead.";
}
