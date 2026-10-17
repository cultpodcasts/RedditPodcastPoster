using RedditPodcastPoster.Models.Cosmos;

namespace CultPodcasts.DatabasePublisher.Consistency;

/// <summary>
/// Checks every file key and id before anything is written. All kinds share one folder, so a key must be
/// present, carry its kind's reserved prefix (podcasts must not use one), and be unique across all kinds
/// ignoring case; an id must belong to one live item only, because a file that moved is recognised by its id.
/// Removed items are included in the key check: the podcast check always covered them, and a removed item can
/// come back.
/// </summary>
public static class PublicFileKeyGuard
{
    public static void EnsureValid(IReadOnlyCollection<CatalogueItem> items)
    {
        var problems = new List<string>();

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.FileKey))
            {
                problems.Add($"{Describe(item)} has a null or whitespace file-key.");
                continue;
            }

            var required = item.Kind.RequiredPrefix();
            if (required is not null)
            {
                if (!item.FileKey.StartsWith(required, StringComparison.Ordinal))
                {
                    problems.Add(
                        $"{Describe(item)} has file-key '{item.FileKey}' which must start with the reserved prefix '{required}'.");
                }
            }
            else if (PublicKindExtensions.UsesReservedPrefix(item.FileKey))
            {
                problems.Add(
                    $"{Describe(item)} has file-key '{item.FileKey}' which starts with a prefix reserved for other kinds ('{FileKeyFactory.FilmPrefix}', '{FileKeyFactory.TvShowPrefix}', '{FileKeyFactory.NewsOrganisationPrefix}').");
            }
        }

        var duplicateKeys = items
            .Where(i => !string.IsNullOrWhiteSpace(i.FileKey))
            .GroupBy(i => i.FileKey, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);
        foreach (var duplicate in duplicateKeys)
        {
            var owners = string.Join(" and ", duplicate.Select(Describe));
            var advice = duplicate.All(i => i.Kind == PublicKind.Film)
                ? $"Give one of the films a different stored file key in Cosmos (it must keep the '{FileKeyFactory.FilmPrefix}' prefix, for example '{duplicate.Key}-2'); the publisher never renames items itself."
                : "Give all but one of them a different stored file key in Cosmos, keeping each kind's prefix; the publisher never renames items itself.";
            problems.Add($"File-key '{duplicate.Key}' is used by more than one item: {owners}. {advice}");
        }

        var duplicateIds = items
            .Where(i => !i.Removed)
            .GroupBy(i => i.Id)
            .Where(g => g.Count() > 1);
        foreach (var duplicate in duplicateIds)
        {
            var owners = string.Join(" and ", duplicate.Select(Describe));
            problems.Add(
                $"Id '{duplicate.Key}' belongs to more than one live item: {owners}. A podcast that was transferred must no longer exist as a podcast; finish or undo the transfer.");
        }

        if (problems.Count > 0)
        {
            throw new PublicDatabaseConsistencyException(problems);
        }
    }

    private static string Describe(CatalogueItem item) =>
        string.IsNullOrWhiteSpace(item.Name)
            ? $"{item.Kind} (id {item.Id})"
            : $"{item.Kind} '{item.Name}' (id {item.Id})";
}
