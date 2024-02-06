using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Reddit;

public class RedditPostResult(
    bool success,
    string message = "",
    bool alreadyPosted = false)
    : MessageResponseBase(success, message)
{
    public bool AlreadyPosted { init; get; } = alreadyPosted;

    public static RedditPostResult Successful(string s = "")
    {
        return new RedditPostResult(true, s);
    }

    public static RedditPostResult Fail(string s)
    {
        return new RedditPostResult(false, s);
    }

    public static RedditPostResult FailAlreadyPosted()
    {
        return new RedditPostResult(false, alreadyPosted: true);
    }
}