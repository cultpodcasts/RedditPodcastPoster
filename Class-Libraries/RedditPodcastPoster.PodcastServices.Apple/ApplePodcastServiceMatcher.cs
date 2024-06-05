namespace RedditPodcastPoster.PodcastServices.Apple;

public static class ApplePodcastServiceMatcher
{
    public static bool IsMatch(Uri url)
    {
        return url.Host.ToLower().Contains("apple");
    }
}