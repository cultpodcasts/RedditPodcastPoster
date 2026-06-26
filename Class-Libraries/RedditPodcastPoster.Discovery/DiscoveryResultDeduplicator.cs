using System.Text;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Discovery;

public class DiscoveryResultDeduplicator : IDiscoveryResultDeduplicator
{
    public IReadOnlyList<DiscoveryResult> Deduplicate(IEnumerable<DiscoveryResult> results)
    {
        var items = results.ToList();
        if (items.Count <= 1)
        {
            return items;
        }

        var parent = Enumerable.Range(0, items.Count).ToArray();
        var keyToIndices = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        for (var index = 0; index < items.Count; index++)
        {
            foreach (var key in GetKeys(items[index]))
            {
                if (!keyToIndices.TryGetValue(key, out var indices))
                {
                    indices = [];
                    keyToIndices[key] = indices;
                }

                indices.Add(index);
            }
        }

        foreach (var indices in keyToIndices.Values)
        {
            for (var i = 1; i < indices.Count; i++)
            {
                Union(parent, indices[0], indices[i]);
            }
        }

        var groups = new Dictionary<int, List<DiscoveryResult>>();
        for (var index = 0; index < items.Count; index++)
        {
            var root = Find(parent, index);
            if (!groups.TryGetValue(root, out var group))
            {
                group = [];
                groups[root] = group;
            }

            group.Add(items[index]);
        }

        return groups.Values.Select(Merge).ToList();
    }

    private static IEnumerable<string> GetKeys(DiscoveryResult result)
    {
        yield return GetNameKey(result);

        if (result.Urls.Spotify != null)
        {
            yield return $"spotify:{NormalizeUrl(result.Urls.Spotify)}";
        }

        if (result.Urls.Apple != null)
        {
            yield return $"apple:{NormalizeUrl(result.Urls.Apple)}";
        }

        if (result.Urls.YouTube != null)
        {
            yield return $"youtube:{NormalizeUrl(result.Urls.YouTube)}";
        }
    }

    private static string GetNameKey(DiscoveryResult result) =>
        $"name:{NormalizeText(result.ShowName)}|{NormalizeText(result.EpisodeName)}";

    private static DiscoveryResult Merge(IReadOnlyList<DiscoveryResult> items)
    {
        var ordered = items
            .OrderByDescending(x => x.AcceptProbability ?? -1f)
            .ThenByDescending(CountUrls)
            .ToList();
        var merged = Clone(ordered[0]);

        foreach (var other in ordered.Skip(1))
        {
            merged.Urls.Spotify ??= other.Urls.Spotify;
            merged.Urls.Apple ??= other.Urls.Apple;
            merged.Urls.YouTube ??= other.Urls.YouTube;
            merged.ImageUrl ??= other.ImageUrl;
            merged.Length ??= other.Length;
        }

        var youTube = items.FirstOrDefault(x => x.Sources.Contains(DiscoverService.YouTube));
        if (youTube != null)
        {
            merged.Released = youTube.Released;
            merged.YouTubeViews = youTube.YouTubeViews;
            merged.YouTubeChannelMembers = youTube.YouTubeChannelMembers;
        }

        var apple = items.FirstOrDefault(x => x.EnrichedTimeFromApple);
        if (apple != null)
        {
            merged.Released = apple.Released;
            merged.EnrichedTimeFromApple = true;
        }

        if (items.Any(x => x.EnrichedUrlFromSpotify))
        {
            merged.EnrichedUrlFromSpotify = true;
        }

        if (string.IsNullOrWhiteSpace(merged.ShowDescription))
        {
            merged.ShowDescription = items
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.ShowDescription))
                ?.ShowDescription;
        }

        if (string.IsNullOrWhiteSpace(merged.Description))
        {
            merged.Description = items
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Description))
                ?.Description;
        }

        merged.AcceptProbability = items.Max(x => x.AcceptProbability ?? -1f);
        if (merged.AcceptProbability < 0)
        {
            merged.AcceptProbability = null;
        }

        merged.AutoHidden = items.All(x => x.AutoHidden);
        merged.Sources = items.SelectMany(x => x.Sources).Distinct().ToArray();
        merged.MatchingPodcastIds = items.SelectMany(x => x.MatchingPodcastIds).Distinct().ToArray();
        merged.Subjects = items.SelectMany(x => x.Subjects).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        return merged;
    }

    private static DiscoveryResult Clone(DiscoveryResult source) =>
        new()
        {
            Id = source.Id,
            EpisodeName = source.EpisodeName,
            ShowName = source.ShowName,
            Released = source.Released,
            Length = source.Length,
            ShowDescription = source.ShowDescription,
            Description = source.Description,
            State = source.State,
            Urls = new DiscoveryResultUrls
            {
                Spotify = source.Urls.Spotify,
                Apple = source.Urls.Apple,
                YouTube = source.Urls.YouTube
            },
            Subjects = source.Subjects.ToArray(),
            YouTubeViews = source.YouTubeViews,
            YouTubeChannelMembers = source.YouTubeChannelMembers,
            ImageUrl = source.ImageUrl,
            Sources = source.Sources.ToArray(),
            EnrichedTimeFromApple = source.EnrichedTimeFromApple,
            EnrichedUrlFromSpotify = source.EnrichedUrlFromSpotify,
            MatchingPodcastIds = source.MatchingPodcastIds.ToArray(),
            AcceptProbability = source.AcceptProbability,
            AutoHidden = source.AutoHidden
        };

    private static int CountUrls(DiscoveryResult result) =>
        (result.Urls.Spotify != null ? 1 : 0) +
        (result.Urls.Apple != null ? 1 : 0) +
        (result.Urls.YouTube != null ? 1 : 0);

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var previousWasWhitespace = false;
        foreach (var character in value.Trim())
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                    previousWasWhitespace = true;
                }

                continue;
            }

            builder.Append(char.ToLowerInvariant(character));
            previousWasWhitespace = false;
        }

        return builder.ToString();
    }

    private static string NormalizeUrl(Uri url) =>
        url.GetLeftPart(UriPartial.Path).TrimEnd('/').ToLowerInvariant();

    private static int Find(int[] parent, int index)
    {
        while (parent[index] != index)
        {
            parent[index] = parent[parent[index]];
            index = parent[index];
        }

        return index;
    }

    private static void Union(int[] parent, int left, int right)
    {
        var leftRoot = Find(parent, left);
        var rightRoot = Find(parent, right);
        if (leftRoot != rightRoot)
        {
            parent[rightRoot] = leftRoot;
        }
    }
}
