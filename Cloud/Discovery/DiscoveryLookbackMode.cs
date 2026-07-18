namespace Discovery;

public enum DiscoveryLookbackMode
{
    /// <summary>
    /// Fixed window: <c>UtcNow - SearchSince</c>. Production uses 6h10m over a 6h schedule for intentional overlap.
    /// </summary>
    Static = 0,

    /// <summary>
    /// Window anchored to the latest successful Discovery run (<c>since = lastSuccess</c>, optional overlap only).
    /// Does not apply the static <c>SearchSince</c> floor when a prior success exists.
    /// Extends further back when runs were missed. Falls back to <see cref="Static"/> when no prior signal exists.
    /// </summary>
    Dynamic = 1
}
