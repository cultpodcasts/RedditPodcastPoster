namespace CultPodcasts.DatabasePublisher.Consistency;

/// <summary>An item as it exists in Cosmos, whether or not it will be published.</summary>
public sealed record CatalogueItem(Guid Id, string FileKey, PublicKind Kind, bool Removed);
