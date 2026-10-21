namespace CultPodcasts.DatabasePublisher.Consistency;

/// <summary>
/// A file already in the output folder: its file key (name without extension) and the id inside it.
/// <paramref name="ChildIds"/> are the episode ids of an unprefixed (podcast) file; they are how a podcast
/// that became a film is recognised, because a film takes the id of the episode it was made from.
/// <paramref name="Length"/> and <paramref name="LastWriteUtcTicks"/> are what the file was when it was read,
/// so that a file changed while the publish was being prepared can be noticed.
/// <paramref name="Releases"/> are the <c>release</c> texts of the file's episodes or reports, read once with the
/// file, so that a release that has not changed keeps its text (see <see cref="Mapping.BaselineReleaseText"/>);
/// null means none.
/// </summary>
public sealed record BaselineFile(
    string FileKey,
    Guid? Id,
    IReadOnlyList<Guid> ChildIds,
    long Length = 0,
    long LastWriteUtcTicks = 0,
    Mapping.BaselineReleaseText? Releases = null);
