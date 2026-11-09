namespace RedditPodcastPoster.UrlSubmission.Categorisation;

/// <summary>
/// G5 (#1211): BBC news content comes in only via BBC iPlayer or the BBC News YouTube channel.
/// A <c>bbc.co.uk/news/…</c> or <c>bbc.com/news/…</c> page is rejected in submit and lookup with
/// <see cref="Message"/>. There is no BBC <c>/news/</c> matcher or scraper, and nothing is saved.
/// </summary>
public static class BbcNewsSubmitRejection
{
    public const string Message =
        "BBC News pages can't be submitted. Submit the BBC iPlayer episode, or the video from the BBC News YouTube channel.";

    public static bool Applies(Uri url) => SubmitContentClassifier.IsBbcNews(url);
}
