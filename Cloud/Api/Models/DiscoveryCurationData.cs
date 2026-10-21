using RedditPodcastPoster.Models.Discovery;

namespace Api.Models;

/// <param name="Kind">
/// Null for a podcast; <c>NewsOrganisation</c> or <c>TvShow</c> when the matched id is a catalogue parent (a migrated
/// podcast keeps its id, so discovery results written before or after the migration resolve).
/// </param>
public record DiscoveryPodcastInfo(string Name, bool IsVisible, int VisibleEpisodes, string? Kind = null);

public record DiscoveryCurationData(
    IEnumerable<Guid> Ids,
    IReadOnlyList<DiscoveryResult> Results,
    IReadOnlyDictionary<Guid, DiscoveryPodcastInfo> Podcasts,
    int HiddenCount,
    IReadOnlyList<DiscoveryAwaitingReview> AwaitingReview);

/// <summary>A report held by a supervised curator's submission; not curatable until a supervisor applies it.</summary>
public record DiscoveryAwaitingReview(Guid DocumentId, DateTime DiscoveryBegan, int ResultCount);
