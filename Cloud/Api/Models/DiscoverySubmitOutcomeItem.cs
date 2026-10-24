namespace Api.Models;

/// <summary>One discovery result's durable submit stamp for client reconcile after a lost response.</summary>
public record DiscoverySubmitOutcomeItem(
    Guid DiscoveryItemId,
    string State,
    Guid? PodcastId = null,
    Guid? EpisodeId = null,
    string? Message = null,
    string? EpisodeName = null,
    string? ShowName = null);
