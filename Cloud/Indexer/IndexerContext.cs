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
        var indexerOperationId = $"indexer-operation-id: '{IndexerOperationId}'";
        var categoriserOperationId = CategoriserOperationId.HasValue
            ? $"categoriser-operation-id: '{CategoriserOperationId}'"
            : string.Empty;
        var posterOperationId = PosterOperationId.HasValue
            ? $"poster-operation-id: '{PosterOperationId}'"
            : string.Empty;
        var publisherOperationId = PublisherOperationId.HasValue
            ? $"publisher-operation-id: '{PublisherOperationId}'"
            : string.Empty;
        var tweetOperationId = TweetOperationId.HasValue
            ? $"tweet-operation-id: '{TweetOperationId}'"
            : string.Empty;
        var success = Success.HasValue
            ? $"success: '{Success}'"
            : string.Empty;
        var skipYouTubeUrlResolving = SkipYouTubeUrlResolving.HasValue
            ? $"skip-youtube-url-resolving: '{SkipYouTubeUrlResolving}'"
            : string.Empty;
        var youTubeError = YouTubeError.HasValue
            ? $"youtube-error: '{YouTubeError}'"
            : string.Empty;
        var skipSpotifyUrlResolving = SkipSpotifyUrlResolving.HasValue
            ? $"skip-spotify-url-resolving: '{SkipSpotifyUrlResolving}'"
            : string.Empty;
        var spotifyError = SpotifyError.HasValue
            ? $"spotify-error: '{SpotifyError}'"
            : string.Empty;
        var duplicateIndexerOperation = DuplicateIndexerOperation.HasValue
            ? $"duplicate-indexer-operation: '{DuplicateIndexerOperation}'"
            : string.Empty;
        var duplicateCategoriserOperation = DuplicateCategoriserOperation.HasValue
            ? $"duplicate-categoriser-operation: '{DuplicateCategoriserOperation}'"
            : string.Empty;
        var duplicatePosterOperation = DuplicatePosterOperation.HasValue
            ? $"duplicate-poster-operation: '{DuplicatePosterOperation}'"
            : string.Empty;
        var duplicatePublisherOperation = DuplicatePublisherOperation.HasValue
            ? $"duplicate-publisher-operation: '{DuplicatePublisherOperation}'"
            : string.Empty;
        var duplicateTweetOperation = DuplicateTweetOperation.HasValue
            ? $"duplicate-tweet-operation: '{DuplicateTweetOperation}'"
            : string.Empty;

        return
            $"{nameof(IndexerContext)} Indexer-options {string.Join(", ", new[] {indexerOperationId, categoriserOperationId, posterOperationId, publisherOperationId, tweetOperationId, success, skipYouTubeUrlResolving, youTubeError, skipSpotifyUrlResolving, spotifyError, duplicateIndexerOperation, duplicateCategoriserOperation, duplicatePosterOperation, duplicatePublisherOperation, duplicateTweetOperation}.Where(x => !string.IsNullOrWhiteSpace(x)))}.";
    }
}