namespace RedditPodcastPoster.EntitySearchIndexer.D1Search;

public enum D1SearchReconcilePhase
{
    Starting,
    Catalogue,
    Inventory,
    Upsert,
    Delete,

    /// <summary>Rebuild (SP-2.3 #226): reading the rebuild state and creating the shadow tables.</summary>
    Prepare,

    /// <summary>Rebuild: documents built from Cosmos and loaded into the shadow tables.</summary>
    Load,

    /// <summary>Rebuild: verifying the new index and swapping it in.</summary>
    Swap,

    /// <summary>Rebuild: integrity check and sample searches on the new live index.</summary>
    Check,

    /// <summary>Rebuild: dropping the old index, or swapping it back.</summary>
    Settle
}

/// <summary>
/// A progress update from <see cref="D1SearchReconciler"/>. The class library does not render UI;
/// the <c>D1SearchReconcile</c> console paints a Spectre.Console bar from this. The Functions timer
/// omits progress.
/// </summary>
/// <param name="Phase">The step that just advanced.</param>
/// <param name="Completed">How many items have been processed in this phase.</param>
/// <param name="Total">
/// The known total, or <c>null</c> while the catalogue is still streaming or the inventory is still
/// paging (the count is only known when that phase ends).
/// </param>
/// <param name="Detail">
/// An optional short label for a wait that has no count yet, such as "waiting for first page" or
/// "authenticating". The console appends it to the task description.
/// </param>
public sealed record D1SearchReconcileProgress(
    D1SearchReconcilePhase Phase,
    int Completed,
    int? Total = null,
    string? Detail = null);
