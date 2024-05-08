using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Reddit;

public class RedditPostResult(
    bool success,
    string message = "",
    bool alreadyPosted = false,
    string? title = null)
    : MessageResponseBase(success, message)
{
    public bool AlreadyPosted { init; get; } = alreadyPosted;
    public string? Title { init; get; } = title;

    public static RedditPostResult Successful(string title)
    {
        return new RedditPostResult(true, title: title);
    }

    public static RedditPostResult Fail(string failureMessage)
    {
        return new RedditPostResult(false, failureMessage);
    }

    public static RedditPostResult FailAlreadyPosted()
    {
        return new RedditPostResult(false, alreadyPosted: true);
    }
}