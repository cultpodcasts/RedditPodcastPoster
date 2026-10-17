using RedditPodcastPoster.Models.Cosmos;

namespace CultPodcasts.DatabasePublisher.Consistency;

/// <summary>
/// Checks every file key before anything is written. All kinds share one folder, so a key must be present,
/// carry its kind's reserved prefix (podcasts must not use one), and be unique across all kinds ignoring case.
/// Removed items are included: the podcast check always covered them, and a removed item can come back.
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
                problems.Add($"{item.Kind} '{item.Id}' has a null or whitespace file-key.");
                continue;
            }

            var required = item.Kind.RequiredPrefix();
            if (required is not null)
            {
                if (!item.FileKey.StartsWith(required, StringComparison.Ordinal))
                {
                    problems.Add(
                        $"{item.Kind} '{item.Id}' has file-key '{item.FileKey}' which must start with the reserved prefix '{required}'.");
                }
            }
            else if (PublicKindExtensions.UsesReservedPrefix(item.FileKey))
            {
                problems.Add(
                    $"{item.Kind} '{item.Id}' has file-key '{item.FileKey}' which starts with a prefix reserved for other kinds ('{FileKeyFactory.FilmPrefix}', '{FileKeyFactory.TvShowPrefix}', '{FileKeyFactory.NewsOrganisationPrefix}').");
            }
        }

        var duplicates = items
            .Where(i => !string.IsNullOrWhiteSpace(i.FileKey))
            .GroupBy(i => i.FileKey, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);
        foreach (var duplicate in duplicates)
        {
            var owners = string.Join(", ", duplicate.Select(i => $"{i.Kind} '{i.Id}'"));
            problems.Add($"File-key '{duplicate.Key}' is used by more than one item: {owners}.");
        }

        if (problems.Count > 0)
        {
            throw new PublicDatabaseConsistencyException(problems);
        }
    }
}
