namespace RedditPodcastPoster.Common;

public class ProcessResponse : MessageResponseBase
{
    public ProcessResponse(bool success, string message = "") : base(success, message)
    {
    }

    public static ProcessResponse Successful(string s = "")
    {
        return new ProcessResponse(true, s);
    }

    public static ProcessResponse AlreadyPosted(string s = "")
    {
        return new ProcessResponse(true, s);
    }

    public static ProcessResponse TooShort(string s = "")
    {
        return new ProcessResponse(true, s);
    }

    public static ProcessResponse NoSuitableLink(string s = "")
    {
        return new ProcessResponse(true, s);
    }

    public static ProcessResponse Fail(string s)
    {
        return new ProcessResponse(false, s);
    }
}