namespace RedditPodcastPoster.UrlSubmission.Categorisation;

/// <summary>
/// A Discovery+ <c>/show/{id}</c> page is a series hub (no episode list ingest).
/// Submit rejects it and tells the submitter to paste an episode URL (#1587).
/// </summary>
public static class DiscoveryPlusSeriesHubSubmitRejection
{
    public const string Message =
        "This is a Discovery+ series page. Submit an episode URL instead (…/show/{id}/s1/e…).";
}
