namespace RedditPodcastPoster.UrlShortening.Configuration;

public class ShortnerOptions
{
    public required Uri ShortnerUrl { get; set; }
    public required string KVShortnerNamespaceId { get; set; }
}
