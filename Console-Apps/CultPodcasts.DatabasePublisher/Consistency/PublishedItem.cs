namespace CultPodcasts.DatabasePublisher.Consistency;

/// <summary>A file this run wrote.</summary>
public sealed record PublishedItem(Guid Id, string FileKey, PublicKind Kind);
