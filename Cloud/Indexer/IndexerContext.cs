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

    public override string ToString()
    {
        var categoriserOperationId = CategoriserOperationId.HasValue
            ? $"categoriser-operation-id: '{CategoriserOperationId}', "
            : "categoriser-operation-id: Null";
        var posterOperationId = PosterOperationId.HasValue
            ? $"poster-operation-id: '{PosterOperationId}', "
            : "poster-operation-id: Null";
        var publisherOperationId = PublisherOperationId.HasValue
            ? $"publisher-operation-id: '{PublisherOperationId}', "
            : "publisher-operation-id: Null";
        var tweetOperationId = TweetOperationId.HasValue
            ? $"tweet-operation-id: '{TweetOperationId}', "
            : "tweet-operation-id: Null";
        var success = Success.HasValue
            ? $"success: '{Success}', "
            : "success: Null";
        var skipYouTubeUrlResolving = SkipYouTubeUrlResolving.HasValue
            ? $"skip-youtube-url-resolving: '{SkipYouTubeUrlResolving}', "
            : "tweet-operation-id: Null";
        var youTubeError = YouTubeError.HasValue
            ? $"youtube-error: '{YouTubeError}', "
            : "youtube-error: Null";
        var skipSpotifyUrlResolving = SkipSpotifyUrlResolving.HasValue
            ? $"youtube-error: '{SkipSpotifyUrlResolving}', "
            : "youtube-error: Null";
        var spotifyError = SpotifyError.HasValue
            ? $"spotify-error: '{SpotifyError}', "
            : "spotify-error: Null";
        var indexerOperationId = $"indexer-operation-id: '{IndexerOperationId}', ";
        var duplicateIndexerOperation = DuplicateIndexerOperation.HasValue
            ? $"duplicate-indexer-operation: '{DuplicateIndexerOperation}', "
            : "duplicate-indexer-operation: Null";
        var duplicateCategoriserOperation = DuplicateCategoriserOperation.HasValue
            ? $"duplicate-categoriser-operation: '{DuplicateCategoriserOperation}', "
            : "duplicate-categoriser-operation: Null";
        var duplicatePosterOperation = DuplicatePosterOperation.HasValue
            ? $"duplicate-poster-operation: '{DuplicatePosterOperation}', "
            : "duplicate-poster-operation: Null";
        var duplicatePublisherOperation = DuplicatePublisherOperation.HasValue
            ? $"duplicate-publisher-operation: '{DuplicatePublisherOperation}', "
            : "duplicate-publisher-operation: Null";
        var duplicateTweetOperation = DuplicateTweetOperation.HasValue
            ? $"duplicate-tweet-operation: '{DuplicateTweetOperation}', "
            : "duplicate-tweet-operation: Null";

        return
            $"{nameof(IndexerContext)} Indexer-options {string.Join(", ", categoriserOperationId, posterOperationId, publisherOperationId, tweetOperationId, success, skipYouTubeUrlResolving, youTubeError, skipSpotifyUrlResolving, spotifyError, indexerOperationId, duplicateIndexerOperation, duplicateCategoriserOperation, duplicatePosterOperation, duplicatePublisherOperation, duplicateTweetOperation)}.";
    }
}