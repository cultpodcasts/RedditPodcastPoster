namespace RedditPodcastPoster.Persistence.Abstractions.FileKeys;

/// <summary>
/// The one place a stored file key is chosen for a podcast, TV show, news organisation or film, on create, on a
/// kind transfer and on a rename that sets the key. The key is never empty: a name with no usable Latin slug (for
/// example Korean only, or emoji only) falls back to a usable fallback slug, then to a slug built from the id. The key
/// is unique (ignoring case) among the other items of the same kind, using the <c>_2</c>, <c>_3</c> suffix.
/// </summary>
public interface IFileKeyAllocator
{
    /// <summary>
    /// A new key for item <paramref name="id"/> of <paramref name="kind"/> called <paramref name="name"/>.
    /// <paramref name="fallbackSlug"/> (unprefixed) is used when the name gives no usable slug, for example the key
    /// a podcast already had when it moves to TV or news. <paramref name="alsoTaken"/> marks keys that are taken but
    /// not stored yet (for example keys this process has handed out for items it has not saved).
    /// </summary>
    Task<string> Allocate(
        FileKeyKind kind,
        string name,
        Guid id,
        string? fallbackSlug = null,
        Func<string, bool>? alsoTaken = null);

    /// <summary>
    /// The key for item <paramref name="id"/> after a rename to <paramref name="name"/> that re-keys the item. A name
    /// with no usable slug keeps <paramref name="currentKey"/> when that is usable; a new key equal to the current one
    /// (ignoring case) keeps the current one; otherwise a unique key is allocated as by <see cref="Allocate"/>.
    /// </summary>
    Task<string> AllocateForRename(FileKeyKind kind, string name, Guid id, string? currentKey);

    /// <summary>
    /// <paramref name="currentKey"/> when it is usable for <paramref name="kind"/> (prefix plus a usable slug);
    /// otherwise a new unique key from <paramref name="name"/>. For renames that keep the stored key, so an empty or
    /// placeholder key is repaired and a good key is never changed.
    /// </summary>
    Task<string> RepairIfUnusable(FileKeyKind kind, string name, Guid id, string? currentKey);
}
