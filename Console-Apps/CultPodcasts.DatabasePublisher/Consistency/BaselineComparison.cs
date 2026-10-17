namespace CultPodcasts.DatabasePublisher.Consistency;

/// <summary>This run's files compared with the previous publish.</summary>
public sealed record BaselineComparison(
    int BaselineFileCount,
    int Unchanged,
    IReadOnlyList<PublishedItem> Added,
    IReadOnlyList<FileRemoval> Removals)
{
    public IReadOnlyList<FileRemoval> Unexplained => Removals.Where(r => !r.Reason.IsExplained()).ToList();
}
