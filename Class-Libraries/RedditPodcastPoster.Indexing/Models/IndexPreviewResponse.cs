namespace RedditPodcastPoster.Indexing.Models;

/// <summary>
/// Outcome of <c>IIndexer.Preview</c>, a dry-run index that saves nothing. The counts are set only when
/// <see cref="IndexStatus"/> is <see cref="Models.IndexStatus.Performed"/>.
/// </summary>
public record IndexPreviewResponse(IndexStatus IndexStatus, int NewEpisodeCount = 0, int IgnoredEpisodeCount = 0);
