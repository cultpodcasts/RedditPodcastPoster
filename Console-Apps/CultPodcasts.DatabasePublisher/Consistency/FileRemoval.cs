namespace CultPodcasts.DatabasePublisher.Consistency;

/// <summary>A file in the output folder that this run does not write again under the same key and id.</summary>
/// <param name="Successors">
/// For <see cref="RemovalReason.Renamed"/> and <see cref="RemovalReason.Migrated"/> the items the file's content
/// moved to; for <see cref="RemovalReason.Replaced"/> the item that now owns the file key.
/// </param>
/// <param name="Accepted">An unexplained removal the operator accepted with <c>--accept-removal</c>.</param>
/// <param name="MoveTo">
/// The file key the file is moved to (so git can follow its history) when it has exactly one successor whose
/// file does not exist yet; otherwise the file is deleted.
/// </param>
public sealed record FileRemoval(
    string FileKey,
    Guid? Id,
    RemovalReason Reason,
    IReadOnlyList<PublishedItem> Successors,
    bool Accepted = false,
    string? MoveTo = null)
{
    /// <summary>Explained by Cosmos, or accepted by the operator: the plan may act on it.</summary>
    public bool IsResolved => Reason.IsExplained() || Accepted;

    public string Describe()
    {
        var file = $"'{FileKey}.json' (id {Id})";
        return Reason switch
        {
            RemovalReason.Renamed =>
                $"{file} is now published as '{Successors[0].FileKey}.json' (same id, same kind).",
            RemovalReason.Migrated when Successors.Count == 1 =>
                $"{file} is now published as {Successors[0].Kind} file '{Successors[0].FileKey}.json' " +
                (Successors[0].Id == Id ? "(same id)." : "(an episode of it became that film)."),
            RemovalReason.Migrated =>
                $"{file} had {Successors.Count} episodes that became films ({string.Join(", ", Successors.Select(s => $"'{s.FileKey}.json'"))}); " +
                "no single file can follow it.",
            RemovalReason.Removed =>
                $"{file} is flagged removed in Cosmos.",
            RemovalReason.NoContent =>
                $"{file} still exists in Cosmos and is not removed, but has no episodes or reports to publish.",
            RemovalReason.Replaced =>
                $"{file} is now published for a different item (id {Successors[0].Id}).",
            RemovalReason.NotFound =>
                $"{file} no longer exists in Cosmos and was not flagged removed.",
            RemovalReason.Unreadable =>
                $"'{FileKey}.json' has no readable id so it cannot be matched to an item.",
            _ => throw new ArgumentOutOfRangeException(nameof(Reason), Reason, "Unknown removal reason.")
        };
    }
}
