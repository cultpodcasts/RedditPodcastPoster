namespace RedditPodcastPoster.PodcastServices.YouTube.Configuration;

public class YouTubeChannelOptions
{
    /// <summary>
    /// When true, channel-only podcasts use the uploads playlist instead of Search.List.
    /// Env: RedditPodcastPoster_YouTubeChannel__PreferUploadsPlaylist=true
    /// </summary>
    public bool PreferUploadsPlaylist { get; set; }
}
