using RedditPodcastPoster.Models.Services;

namespace CataloguePlayableServicesBackfill;

/// <summary>
/// Builds the truth services map from episode backup (url + image) and optional PublicDatabase urls.
/// Episode-backup values win when both sources have a non-null url for the same key; public urls fill gaps.
/// Public files have no images — images come only from the episode backup.
/// </summary>
public static class TruthServicesMerger
{
    public static Dictionary<string, ServiceLink> Merge(
        Dictionary<string, ServiceLink>? episodeBackup,
        IReadOnlyDictionary<string, Uri>? publicUrls)
    {
        var map = PlayableServicesDiscrepancyCarry.CloneServices(episodeBackup)
                  ?? new Dictionary<string, ServiceLink>(StringComparer.Ordinal);

        if (publicUrls is null)
        {
            return DropEmpty(map);
        }

        foreach (var (key, url) in publicUrls)
        {
            if (string.IsNullOrWhiteSpace(key) || url is null)
            {
                continue;
            }

            if (!map.TryGetValue(key, out var link))
            {
                map[key] = new ServiceLink { Url = url };
                continue;
            }

            if (link.Url is null)
            {
                link.Url = url;
            }
        }

        return DropEmpty(map);
    }

    private static Dictionary<string, ServiceLink> DropEmpty(Dictionary<string, ServiceLink> map)
    {
        foreach (var key in map.Keys.ToArray())
        {
            if (map[key].IsEmpty)
            {
                map.Remove(key);
            }
        }

        return map;
    }
}
