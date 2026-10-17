namespace CultPodcasts.DatabasePublisher.Consistency;

/// <summary>
/// A file already in the output folder: its file key (name without extension) and the id inside it.
/// <paramref name="ChildIds"/> are the episode ids of an unprefixed (podcast) file; they are how a podcast
/// that became a film is recognised, because a film takes the id of the episode it was made from.
/// </summary>
public sealed record BaselineFile(string FileKey, Guid? Id, IReadOnlyList<Guid> ChildIds);
