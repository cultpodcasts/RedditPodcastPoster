namespace Indexer;

public record IndexerContext(
    Guid IndexerPass1OperationId,
    Guid IndexerPass2OperationId,
    IndexIds? IndexIds = null,
    Guid? CategoriserOperationId = null,
    Guid? PosterOperationId = null,
    Guid? PublisherOperationId = null,
    Guid? TweetOperationId = null,
    Guid? BlueskyOperationId = null,
    bool? Success = null,
    bool? SkipYouTubeUrlResolving = null,
    bool? YouTubeError = null,
    bool? SkipSpotifyUrlResolving = null,
    bool? SpotifyError = null,
    bool? DuplicateIndexerOperation = null,
    bool? DuplicateCategoriserOperation = null,
    bool? DuplicatePosterOperation = null,
    bool? DuplicatePublisherOperation = null,
    bool? DuplicateTweetOperation = null,
    bool? DuplicateBlueskyOperation = null)
{
    public override string ToString()
    {
        var indexerPass1OperationId = $"indexer-pass-1-operation-id: '{IndexerPass1OperationId}'";
        var indexerPass2OperationId = $"indexer-pass-2-operation-id: '{IndexerPass2OperationId}'";
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
        var blueskyOperationId = BlueskyOperationId.HasValue
            ? $"bluesky-operation-id: '{BlueskyOperationId}'"
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
        var duplicateBlueskyOperation = DuplicateBlueskyOperation.HasValue
            ? $"duplicate-bluesky-operation: '{DuplicateBlueskyOperation}'"
            : string.Empty;

        return
            $"{nameof(IndexerContext)} Indexer-options {string.Join(", ", new[] {indexerPass1OperationId, indexerPass2OperationId, categoriserOperationId, posterOperationId, publisherOperationId, tweetOperationId, blueskyOperationId, success, skipYouTubeUrlResolving, youTubeError, skipSpotifyUrlResolving, spotifyError, duplicateIndexerOperation, duplicateCategoriserOperation, duplicatePosterOperation, duplicatePublisherOperation, duplicateTweetOperation, duplicateBlueskyOperation}.Where(x => !string.IsNullOrWhiteSpace(x)))}.";
    }
}