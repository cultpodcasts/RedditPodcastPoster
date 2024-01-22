namespace RedditPodcastPoster.Models;

public class MessageResponseBase(bool success, string message = "")
{
    public bool Success { get; } = success;
    public string Message { get; } = message;


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