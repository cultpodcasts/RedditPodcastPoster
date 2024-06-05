namespace RedditPodcastPoster.PodcastServices.YouTube;

public static class YouTubePodcastServiceMatcher
{
    public static bool IsMatch(Uri url)
    {
        return url.Host.ToLower().Contains("youtube");
    }
}