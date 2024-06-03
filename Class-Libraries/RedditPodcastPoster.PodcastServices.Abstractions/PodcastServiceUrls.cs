namespace RedditPodcastPoster.PodcastServices.Abstractions;

public class PodcastServiceUrls
{
    public Uri? Spotify { get; set; } = null;
    public Uri? Apple { get; set; } = null;
    public Uri? YouTube { get; set; } = null;

    public bool Any()
    {
        return Spotify != null || Apple != null || YouTube != null;
    }
}