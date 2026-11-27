using RedditPodcastPoster.Configuration.Options;

namespace Indexer.Activities;

/// <param name="PodcastIdBatches">Auto-indexed podcast ids per pass.</param>
/// <param name="CatalogueParentIdBatches">
/// Auto-indexed TvShow / NewsOrganisation ids per pass (same split), or null from older runs.
/// </param>
/// <param name="ParallelPasses">
/// Soak-test mode from <c>indexer__ParallelPasses</c>. The orchestrator uses this value and must not
/// read configuration itself (Durable replay determinism).
/// </param>
public record IndexIdProviderResponse(
    Guid[][] PodcastIdBatches,
    Guid[][]? CatalogueParentIdBatches = null,
    ParallelPassesMode ParallelPasses = ParallelPassesMode.Off);
