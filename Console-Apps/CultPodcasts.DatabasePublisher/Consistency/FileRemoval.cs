namespace CultPodcasts.DatabasePublisher.Consistency;

/// <summary>A file from the previous publish that this run does not write under the same key.</summary>
/// <param name="Successor">
/// For <see cref="RemovalReason.Renamed"/> and <see cref="RemovalReason.Migrated"/> the file the item moved to;
/// for <see cref="RemovalReason.Replaced"/> the item that now owns the file key.
/// </param>
public sealed record FileRemoval(string FileKey, Guid? Id, RemovalReason Reason, PublishedItem? Successor)
{
    public string Describe()
    {
        var file = $"'{FileKey}.json' (id {Id})";
        return Reason switch
        {
            RemovalReason.Renamed =>
                $"{file} is now published as '{Successor!.FileKey}.json' (same id, same kind).",
            RemovalReason.Migrated =>
                $"{file} is now published as {Successor!.Kind} file '{Successor.FileKey}.json' (same id).",
            RemovalReason.Removed =>
                $"{file} is flagged removed in Cosmos.",
            RemovalReason.NoContent =>
                $"{file} still exists in Cosmos and is not removed, but has no episodes or reports to publish.",
            RemovalReason.Replaced =>
                $"{file} is now published for a different item (id {Successor!.Id}).",
            RemovalReason.NotFound =>
                $"{file} no longer exists in Cosmos and was not flagged removed.",
            RemovalReason.Unreadable =>
                $"'{FileKey}.json' has no readable id so it cannot be matched to an item.",
            _ => throw new ArgumentOutOfRangeException(nameof(Reason), Reason, "Unknown removal reason.")
        };
    }
}
