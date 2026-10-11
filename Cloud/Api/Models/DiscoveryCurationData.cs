using RedditPodcastPoster.Models.Discovery;

namespace Api.Models;

public record DiscoveryPodcastInfo(string Name, bool IsVisible, int VisibleEpisodes);

public record DiscoveryCurationData(
    IEnumerable<Guid> Ids,
    IReadOnlyList<DiscoveryResult> Results,
    IReadOnlyDictionary<Guid, DiscoveryPodcastInfo> Podcasts,
    int HiddenCount,
    IReadOnlyList<DiscoveryAwaitingReview> AwaitingReview);

/// <summary>A report held by a supervised curator's submission; not curatable until a supervisor applies it.</summary>
public record DiscoveryAwaitingReview(Guid DocumentId, DateTime DiscoveryBegan, int ResultCount);
