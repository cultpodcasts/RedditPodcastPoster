using RedditPodcastPoster.EntitySearchIndexer.Models;

namespace Api.Models;

public record DiscoverySubmitItemOutcome(
    Guid DiscoveryItemId,
    Guid? EpisodeId,
    Guid? PodcastId,
    string Message);

public record DiscoverySubmitOutcome(
    string Message,
    bool ErrorsOccurred,
    IReadOnlyList<DiscoverySubmitItemOutcome> Results,
    EntitySearchIndexerResponse SearchIndexer);
