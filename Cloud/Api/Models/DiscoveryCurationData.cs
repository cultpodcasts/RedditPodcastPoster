using RedditPodcastPoster.Models.Discovery;

namespace Api.Models;

public record DiscoveryPodcastInfo(string Name, bool IsVisible, int VisibleEpisodes);

public record DiscoveryCurationData(
    IEnumerable<Guid> Ids,
    IReadOnlyList<DiscoveryResult> Results,
    IReadOnlyDictionary<Guid, DiscoveryPodcastInfo> Podcasts,
    int HiddenCount);
