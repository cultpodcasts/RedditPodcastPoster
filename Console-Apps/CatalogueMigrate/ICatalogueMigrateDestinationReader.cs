namespace CatalogueMigrate;

/// <summary>
/// Read-only view of the destination containers. Needs only the Cosmos repositories, so dry-run planning and a
/// rollback preview never build the Search client (<see cref="ICatalogueMigrateDestination"/> does).
/// </summary>
public interface ICatalogueMigrateDestinationReader
{
    /// <summary>Playables stored under the parent. Film: 1 when the film exists (parentId is the film id).</summary>
    Task<int> CountPlayables(string kind, Guid parentId);

    /// <summary>
    /// Id of an existing TV show / news organisation (per <paramref name="kind"/>) whose name or fileKey matches,
    /// other than <paramref name="excludeId"/>; null when there is none. Always null for Film (no parent).
    /// </summary>
    Task<Guid?> FindOtherParentWithName(string kind, string name, string fileKey, Guid excludeId);
}
