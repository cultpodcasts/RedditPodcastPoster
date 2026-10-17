namespace CultPodcasts.DatabasePublisher.Consistency;

/// <summary>An item this run publishes.</summary>
public sealed record PublishedItem(Guid Id, string FileKey, PublicKind Kind);
