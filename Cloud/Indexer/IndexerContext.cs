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
    public bool? DuplicateIndexerOperation { get; set; }
    public bool? DuplicateCategoriserOperation { get; set; }
    public bool? DuplicatePosterOperation { get; set; }
    public bool? DuplicatePublisherOperation { get; set; }
    public bool? DuplicateTweetOperation { get; set; }
}