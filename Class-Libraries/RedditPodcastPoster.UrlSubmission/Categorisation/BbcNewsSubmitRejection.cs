using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Categorisation;

/// <summary>
/// G5 (#1211): BBC news content comes in only via BBC iPlayer or the BBC News YouTube channel.
/// A BBC News page is rejected in submit and lookup with <see cref="Message"/>. There is no BBC
/// <c>/news/</c> matcher or scraper, and nothing is saved.
/// </summary>
public static class BbcNewsSubmitRejection
{
    private const string LegacyNewsHost = "news.bbc.co.uk";

    public const string Message =
        "BBC News pages can't be submitted. Submit the BBC iPlayer episode, or the video from the BBC News YouTube channel.";

    /// <summary>
    /// True for <c>bbc.co.uk/news/…</c> and <c>bbc.com/news/…</c> (any subdomain, see
    /// <see cref="SubmitContentClassifier.IsBbcNews"/>), the bare <c>/news</c> section page on those hosts,
    /// and any page on the legacy <c>news.bbc.co.uk</c> host. Deliberately wider than <c>IsBbcNews</c>, which
    /// the classifier and catalogue migrate still use unchanged.
    /// </summary>
    public static bool Applies(Uri url)
    {
        if (!url.IsAbsoluteUri)
        {
            return false;
        }

        if (SubmitContentClassifier.IsBbcNews(url))
        {
            return true;
        }

        if (HostIs(url, LegacyNewsHost))
        {
            return true;
        }

        return (HostIs(url, "bbc.co.uk") || HostIs(url, "bbc.com"))
               && url.AbsolutePath.Equals("/news", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The submit outcome for a rejected BBC News page: nothing created, kind NewsReport, rejected with <see cref="Message"/>.
    /// </summary>
    public static SubmitResult Result() =>
        new(
            SubmitResultState.None,
            SubmitResultState.None,
            ContentKind: SubmitClassification.NewsReport,
            Rejected: true,
            RejectionMessage: Message);

    private static bool HostIs(Uri url, string host) =>
        url.Host.Equals(host, StringComparison.OrdinalIgnoreCase)
        || url.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase);
}
