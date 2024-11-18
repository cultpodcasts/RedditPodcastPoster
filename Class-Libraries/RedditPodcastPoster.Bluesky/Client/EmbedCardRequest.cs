namespace RedditPodcastPoster.Bluesky.Client;

public class EmbedCardRequest(string title, string description, Uri url, Uri? thumbUrl = null)
{
    public string Description { get; } = description;
    public Uri Url { get; } = url;
    public string Title { get; } = title;
    public Uri? ThumbUrl { set; get; } = thumbUrl;
}