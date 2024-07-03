namespace RedditPodcastPoster.UrlShortening;

public static class GuidExtensions
{
    public static string ToBase64(this Guid guid)
    {
        return Convert.ToBase64String(guid.ToByteArray())
            .Replace("/", "-")
            .Replace("+", "_")
            .Replace("=", "");
    }
}