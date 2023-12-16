namespace Indexer;

public class IndexerContext
{
    public IndexerContext(Guid indexerOperationId)
    {
        IndexerOperationId = indexerOperationId;
    }

    private IndexerContext(
        Guid indexerOperationId,
        bool success,
        bool skipYouTubeUrlResolving,
        bool youTubeError,
        bool skipSpotifyUrlResolving,
        bool spotifyError)
    {
        IndexerOperationId = indexerOperationId;
        Success = success;
        SkipYouTubeUrlResolving = skipYouTubeUrlResolving;
        YouTubeError = youTubeError;
        SkipSpotifyUrlResolving = skipSpotifyUrlResolving;
        SpotifyError = spotifyError;
    }

    public Guid? CategoriserOperationId { get; private set; }
    public Guid? PosterOperationId { get; private set; }
    public Guid? PublisherOperationId { get; private set; }
    public Guid? TweetOperationId { get; private set; }
    public bool? Success { get; private set; }
    public bool? SkipYouTubeUrlResolving { get; private set; }
    public bool? YouTubeError { get; private set; }
    public bool? SkipSpotifyUrlResolving { get; private set; }
    public bool? SpotifyError { get; private set; }
    public Guid IndexerOperationId { get; init; }

    public IndexerContext CompleteIndexerOperation(
        bool success,
        bool skipYouTubeUrlResolving,
        bool youTubeError,
        bool skipSpotifyUrlResolving,
        bool spotifyError)

    {
        Success = success;
        SkipYouTubeUrlResolving = skipYouTubeUrlResolving;
        YouTubeError = youTubeError;
        SkipSpotifyUrlResolving = skipSpotifyUrlResolving;
        SpotifyError = spotifyError;
        return this;
    }

    public IndexerContext WithCategoriserOperationId(Guid categoriserOperationId)
    {
        CategoriserOperationId = categoriserOperationId;
        return this;
    }

    public IndexerContext WithPosterOperationId(Guid posterOperationId)
    {
        PosterOperationId = posterOperationId;
        return this;
    }

    public IndexerContext WithPublisherOperationId(Guid publisherOperationId)
    {
        PublisherOperationId = publisherOperationId;
        return this;
    }

    public IndexerContext WithTweetOperationId(Guid tweetOperationId)
    {
        TweetOperationId = tweetOperationId;
        return this;
    }
}