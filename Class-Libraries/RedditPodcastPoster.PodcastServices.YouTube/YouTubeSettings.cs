namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeSettings
{
    public required Application[] Applications { get; set; }
}

public class Application
{
    public required string ApiKey { get; set; }
    public required string Name { get; set; }
}