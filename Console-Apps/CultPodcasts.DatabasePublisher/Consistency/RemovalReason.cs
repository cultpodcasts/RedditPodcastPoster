namespace CultPodcasts.DatabasePublisher.Consistency;

/// <summary>Why a file already in the output folder is not written again under the same key.</summary>
public enum RemovalReason
{
    /// <summary>The item is published under a different file key in the same kind.</summary>
    Renamed,

    /// <summary>
    /// The item is published as a different kind: a podcast transferred to a TV show or news organisation (same
    /// id), or a podcast whose episode became a film (the film has the episode's id).
    /// </summary>
    Migrated,

    /// <summary>The item is flagged removed in Cosmos.</summary>
    Removed,

    /// <summary>The item still exists, is not removed, but has no episodes or reports to publish.</summary>
    NoContent,

    /// <summary>The file key is now published for a different item.</summary>
    Replaced,

    /// <summary>The item is gone from Cosmos and was not flagged removed first.</summary>
    NotFound,

    /// <summary>The file has no readable id, so it cannot be matched.</summary>
    Unreadable
}

public static class RemovalReasonExtensions
{
    /// <summary>Renames, migrations and removal flags are expected; everything else is a surprise to look at.</summary>
    public static bool IsExplained(this RemovalReason reason) =>
        reason is RemovalReason.Renamed or RemovalReason.Migrated or RemovalReason.Removed;
}
