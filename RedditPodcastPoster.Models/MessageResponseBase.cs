namespace RedditPodcastPoster.Models;

public class MessageResponseBase
{
    public MessageResponseBase(bool success, string message = "")
    {
        Success = success;
        Message = message;
    }

    public bool Success { get; }
    public string Message { get; }


    public override string ToString()
    {
        if (Success)
        {
            return $"Success : {Message}";
        }

        return $"FAILURE: {Message}";
    }

    public int ToResultCode()
    {
        return Success ? 0 : 1;
    }
}