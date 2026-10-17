namespace CultPodcasts.DatabasePublisher.Consistency;

public static class PublicationPlanner
{
    /// <summary>
    /// Matches every file already in the output folder to this run's items by file key and id, decides what
    /// happens to each (replace, move, delete, or refuse), and decides for every file written whether it
    /// creates or replaces. A file whose item moved to another file is moved there so its content stays comparable for git,
    /// including a podcast whose episodes all became films (a film has its episode's id); a podcast file where only\n    /// some of the episodes did is not treated as migrated.
    /// </summary>
    public static PublicationPlan Plan(
        IReadOnlyCollection<BaselineFile> baseline,
        IReadOnlyCollection<PublishedItem> published,
        IReadOnlyCollection<CatalogueItem> inventory,
        IReadOnlyCollection<string> accepted)
    {
        // Keys are matched exactly: a key that differs only by case is a different file name, so it is a delete and a create.
        var publishedByKey = published.ToDictionary(p => p.FileKey, StringComparer.Ordinal);
        var publishedById = published.OrderBy(p => p.FileKey, StringComparer.Ordinal).ToLookup(p => p.Id);
        var filmsById = published.Where(p => p.Kind == PublicKind.Film).ToDictionary(p => p.Id);
        var inventoryById = inventory.ToLookup(i => i.Id);
        var baselineKeys = baseline.Select(b => b.FileKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var claimedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedAcceptances = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var removals = new List<FileRemoval>();
        foreach (var file in baseline.OrderBy(b => b.FileKey, StringComparer.Ordinal))
        {
            var removal = Classify(file);
            if (removal is null)
            {
                continue;
            }

            if (!removal.Reason.IsExplained())
            {
                var token = accepted.FirstOrDefault(a => Matches(a, removal));
                if (token is not null)
                {
                    usedAcceptances.Add(token);
                    removal = removal with { Accepted = true };
                }
            }

            removals.Add(removal);
        }

        var deletedKeys = removals
            .Where(r => r.IsResolved && r.MoveTo is null)
            .Select(r => r.FileKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var movedByTarget = removals
            .Where(r => r.IsResolved && r.MoveTo is not null)
            .ToDictionary(r => r.MoveTo!, StringComparer.OrdinalIgnoreCase);
        var files = published
            .OrderBy(p => p.FileKey, StringComparer.Ordinal)
            .Select(item =>
                movedByTarget.TryGetValue(item.FileKey, out var moved)
                    ? new PlannedFile(item, WriteMode.Replace, moved.FileKey)
                    : baselineKeys.Contains(item.FileKey) && !deletedKeys.Contains(item.FileKey)
                        ? new PlannedFile(item, WriteMode.Replace, null)
                        : new PlannedFile(item, WriteMode.Create, null))
            .ToList();

        var unused = accepted.Where(a => !usedAcceptances.Contains(a)).ToList();
        return new PublicationPlan(baseline.Count, files, removals, unused);

        FileRemoval? Classify(BaselineFile file)
        {
            if (file.Id is not { } id)
            {
                return new FileRemoval(file.FileKey, null, RemovalReason.Unreadable, []);
            }

            if (publishedByKey.TryGetValue(file.FileKey, out var sameKey))
            {
                return sameKey.Id == id
                    ? null
                    : new FileRemoval(file.FileKey, id, RemovalReason.Replaced, [sameKey]);
            }

            if (publishedById[id].FirstOrDefault() is { } moved)
            {
                var reason = PublicKindExtensions.FromFileKey(file.FileKey) == moved.Kind
                    ? RemovalReason.Renamed
                    : RemovalReason.Migrated;
                return new FileRemoval(file.FileKey, id, reason, [moved], MoveTo: MoveTargetFor([moved]));
            }

            var existing = inventoryById[id].ToList();
            var podcastStillLive = existing.Any(i => !i.Removed);
            if (!podcastStillLive && PublicKindExtensions.FromFileKey(file.FileKey) == PublicKind.Podcast)
            {
                var films = file.ChildIds
                    .Distinct()
                    .Where(filmsById.ContainsKey)
                    .Select(childId => filmsById[childId])
                    .OrderBy(f => f.FileKey, StringComparer.Ordinal)
                    .ToList();
                var episodeCount = file.ChildIds.Distinct().Count();
                if (films.Count > 0 && films.Count == episodeCount)
                {
                    return new FileRemoval(file.FileKey, id, RemovalReason.Migrated, films, MoveTo: MoveTargetFor(films));
                }

                // Only some of the episodes became films and Cosmos does not say what happened to the podcast
                // or the rest, so the file is not treated as migrated: it must be accepted knowingly.
                if (films.Count > 0 && existing.Count == 0)
                {
                    return new FileRemoval(
                        file.FileKey, id, RemovalReason.NotFound, films, UnaccountedChildren: episodeCount - films.Count);
                }
            }

            var reason2 = existing.Count == 0 ? RemovalReason.NotFound
                : podcastStillLive ? RemovalReason.NoContent
                : RemovalReason.Removed;
            return new FileRemoval(file.FileKey, id, reason2, []);
        }

        // A file is moved only when it has exactly one successor, that successor's file is not already there, and
        // no other file has claimed the name; otherwise it is deleted and its successors are created.
        string? MoveTargetFor(IReadOnlyList<PublishedItem> successors) =>
            successors is [{ } only] && !baselineKeys.Contains(only.FileKey) && claimedTargets.Add(only.FileKey)
                ? only.FileKey
                : null;
    }

    private static bool Matches(string token, FileRemoval removal) =>
        token.Equals(removal.FileKey, StringComparison.OrdinalIgnoreCase) ||
        (removal.Id is { } id && token.Equals(id.ToString(), StringComparison.OrdinalIgnoreCase));
}
