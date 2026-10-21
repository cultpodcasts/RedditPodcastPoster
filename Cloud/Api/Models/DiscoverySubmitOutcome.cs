using RedditPodcastPoster.EntitySearchIndexer.Models;

namespace Api.Models;

public record DiscoverySubmitItemOutcome(
    Guid DiscoveryItemId,
    Guid? EpisodeId,
    Guid? PodcastId,
    string Message,
    string? Error = null);

public record DiscoverySubmitOutcome(
    string Message,
    bool ErrorsOccurred,
    IReadOnlyList<DiscoverySubmitItemOutcome> Results,
    EntitySearchIndexerResponse SearchIndexer);

public static class DiscoverySubmitItemOutcomeMessages
{
    /// <summary><see cref="DiscoverySubmitItemOutcome.Message"/> for an item whose submit threw.</summary>
    public const string Error = "Error";
}
