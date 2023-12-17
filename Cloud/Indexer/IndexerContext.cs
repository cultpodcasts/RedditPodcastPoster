namespace Indexer;

public record IndexerContext
{
    public Guid? CategoriserOperationId { get; set; }
    public Guid? PosterOperationId { get; set; }
    public Guid? PublisherOperationId { get; set; }
    public Guid? TweetOperationId { get; set; }
    public bool? Success { get; set; }
    public bool? SkipYouTubeUrlResolving { get; set; }
    public bool? YouTubeError { get; set; }
    public bool? SkipSpotifyUrlResolving { get; set; }
    public bool? SpotifyError { get; set; }
    public Guid IndexerOperationId { get; set; }
}