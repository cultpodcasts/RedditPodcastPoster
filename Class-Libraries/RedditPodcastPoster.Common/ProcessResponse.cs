using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common;

public class ProcessResponse(bool success, string message = "") : MessageResponseBase(success, message)
{
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