namespace RedditPodcastPoster.Common.Reddit;

public class RedditPostResult : MessageResponseBase
{
    public  bool AlreadyPosted { init; get; }

    public RedditPostResult(bool success, string message = "", bool alreadyPosted = false) : base(success, message)
    {
        AlreadyPosted = alreadyPosted;
    }

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