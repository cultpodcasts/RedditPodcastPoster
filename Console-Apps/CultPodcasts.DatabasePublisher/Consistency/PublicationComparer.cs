namespace CultPodcasts.DatabasePublisher.Consistency;

public static class PublicationComparer
{
    /// <summary>
    /// Matches every file of the previous publish to this run by file key and id, and explains each one that
    /// is not written again under the same key and id.
    /// </summary>
    public static BaselineComparison Compare(
        IReadOnlyCollection<BaselineFile> baseline,
        IReadOnlyCollection<PublishedItem> published,
        IReadOnlyCollection<CatalogueItem> inventory)
    {
        var publishedByKey = published.ToDictionary(p => p.FileKey, StringComparer.OrdinalIgnoreCase);
        var publishedById = published.ToLookup(p => p.Id);
        var inventoryById = inventory.ToLookup(i => i.Id);
        var baselineKeys = baseline.Select(b => b.FileKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unchanged = 0;
        var removals = new List<FileRemoval>();
        foreach (var file in baseline)
        {
            if (file.Id is not { } id)
            {
                removals.Add(new FileRemoval(file.FileKey, null, RemovalReason.Unreadable, null));
            }
            else if (publishedByKey.TryGetValue(file.FileKey, out var sameKey))
            {
                if (sameKey.Id == id)
                {
                    unchanged++;
                }
                else
                {
                    removals.Add(new FileRemoval(file.FileKey, id, RemovalReason.Replaced, sameKey));
                }
            }
            else if (publishedById[id].FirstOrDefault() is { } moved)
            {
                var reason = PublicKindExtensions.FromFileKey(file.FileKey) == moved.Kind
                    ? RemovalReason.Renamed
                    : RemovalReason.Migrated;
                removals.Add(new FileRemoval(file.FileKey, id, reason, moved));
            }
            else
            {
                var existing = inventoryById[id].ToList();
                var reason = existing.Count == 0 ? RemovalReason.NotFound
                    : existing.All(i => i.Removed) ? RemovalReason.Removed
                    : RemovalReason.NoContent;
                removals.Add(new FileRemoval(file.FileKey, id, reason, null));
            }
        }

        var added = published
            .Where(p => !baselineKeys.Contains(p.FileKey))
            .OrderBy(p => p.FileKey, StringComparer.Ordinal)
            .ToList();
        return new BaselineComparison(baseline.Count, unchanged, added, removals);
    }
}
