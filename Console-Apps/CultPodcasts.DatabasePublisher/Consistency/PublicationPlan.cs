namespace CultPodcasts.DatabasePublisher.Consistency;

/// <summary>A file this run writes: <see cref="WriteMode.Replace"/> only where a file is already there to replace.</summary>
/// <param name="MovedFromKey">The existing file that is moved to this file's name first, when a rename or migration lands here.</param>
public sealed record PlannedFile(PublishedItem Item, WriteMode Mode, string? MovedFromKey);

/// <summary>
/// Everything a run is going to do to the output folder, worked out before any file is touched: the files it
/// writes (and whether each creates or replaces), and what happens to every existing file that is not
/// written again under the same key.
/// </summary>
public sealed record PublicationPlan(
    int BaselineFileCount,
    IReadOnlyList<PlannedFile> Files,
    IReadOnlyList<FileRemoval> Removals,
    IReadOnlyList<string> UnusedAcceptances)
{
    public IReadOnlyList<FileRemoval> Unexplained => Removals.Where(r => !r.IsResolved).ToList();

    public IReadOnlyList<FileRemoval> Moves => Removals.Where(r => r.IsResolved && r.MoveTo is not null).ToList();

    public IReadOnlyList<FileRemoval> Deletions => Removals.Where(r => r.IsResolved && r.MoveTo is null).ToList();

    /// <summary>Something would vanish without explanation, or an <c>--accept-removal</c> matched nothing.</summary>
    public bool IsBlocked => Unexplained.Count > 0 || UnusedAcceptances.Count > 0;
}
