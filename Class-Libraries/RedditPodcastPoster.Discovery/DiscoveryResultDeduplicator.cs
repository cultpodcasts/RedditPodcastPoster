using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;

namespace RedditPodcastPoster.Discovery;

public class DiscoveryResultDeduplicator : IDiscoveryResultDeduplicator
{
    public IReadOnlyList<DiscoveryResult> Deduplicate(IEnumerable<DiscoveryResult> results)
    {
        var items = results.ToList();
        if (items.Count <= 1)
        {
            return items.Select(Clone).ToList();
        }

        var parent = Enumerable.Range(0, items.Count).ToArray();
        var keyToIndices = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        for (var index = 0; index < items.Count; index++)
        {
            foreach (var key in GetUrlKeys(items[index]))
            {
                if (!keyToIndices.TryGetValue(key, out var indices))
                {
                    indices = [];
                    keyToIndices[key] = indices;
                }

                indices.Add(index);
            }
        }

        foreach (var (_, indices) in keyToIndices)
        {
            for (var i = 0; i < indices.Count; i++)
            {
                for (var j = i + 1; j < indices.Count; j++)
                {
                    Union(parent, indices[i], indices[j]);
                }
            }
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (CountUrls(items[i]) != 0)
            {
                continue;
            }

            for (var j = i + 1; j < items.Count; j++)
            {
                if (CountUrls(items[j]) == 0 && HasSameEpisodeIdentity(items[i], items[j]))
                {
                    Union(parent, i, j);
                }
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

    private static IEnumerable<string> GetUrlKeys(DiscoveryResult result)
    {
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

    private static DiscoveryResult Merge(IReadOnlyList<DiscoveryResult> items)
    {
        if (items.Count == 1)
        {
            return Clone(items[0]);
        }

        var winner = SelectWinner(items);
        var linkedItems = GetLinkedItems(items, winner);
        var merged = Clone(winner);

        merged.Urls.Spotify = PickPlatformUrl(items, linkedItems, winner, x => x.Urls.Spotify, "spotify");
        merged.Urls.Apple = PickPlatformUrl(items, linkedItems, winner, x => x.Urls.Apple, "apple");
        merged.Urls.YouTube = PickPlatformUrl(items, linkedItems, winner, x => x.Urls.YouTube, "youtube");

        foreach (var other in linkedItems.Where(x =>
                     !ReferenceEquals(x, winner) && HasSameEpisodeIdentity(x, winner)))
        {
            merged.ImageUrl ??= other.ImageUrl;
            merged.Length ??= other.Length;
        }

        var youTube = linkedItems.FirstOrDefault(x =>
            x.Sources.Contains(DiscoverService.YouTube) && HasSameEpisodeIdentity(x, winner));
        if (youTube != null)
        {
            merged.Released = youTube.Released;
            merged.YouTubeViews = youTube.YouTubeViews;
            merged.YouTubeChannelMembers = youTube.YouTubeChannelMembers;
        }

        var apple = linkedItems.FirstOrDefault(x =>
            x.EnrichedTimeFromApple && HasSameEpisodeIdentity(x, winner));
        if (apple != null)
        {
            merged.Released = apple.Released;
            merged.EnrichedTimeFromApple = true;
        }

        if (linkedItems.Any(x => x.EnrichedUrlFromSpotify && HasSameEpisodeIdentity(x, winner)))
        {
            merged.EnrichedUrlFromSpotify = true;
        }

        if (string.IsNullOrWhiteSpace(merged.ShowDescription))
        {
            merged.ShowDescription = linkedItems
                .Where(HasSameEpisodeIdentityWith(winner))
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.ShowDescription))
                ?.ShowDescription;
        }

        if (string.IsNullOrWhiteSpace(merged.Description))
        {
            merged.Description = linkedItems
                .Where(HasSameEpisodeIdentityWith(winner))
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Description))
                ?.Description;
        }

        merged.Sources = linkedItems
            .Where(HasSameEpisodeIdentityWith(winner))
            .SelectMany(x => x.Sources)
            .Distinct()
            .ToArray();

        return merged;
    }

    private static IReadOnlyList<DiscoveryResult> GetLinkedItems(
        IReadOnlyList<DiscoveryResult> items,
        DiscoveryResult winner) =>
        items.Where(x => ReferenceEquals(x, winner) || SharesAnyUrlKey(winner, x)).ToList();

    private static Uri? PickPlatformUrl(
        IReadOnlyList<DiscoveryResult> items,
        IReadOnlyList<DiscoveryResult> linkedItems,
        DiscoveryResult winner,
        Func<DiscoveryResult, Uri?> getUrl,
        string platformPrefix)
    {
        var winnerUrl = getUrl(winner);
        if (winnerUrl != null && IsUrlAdoptable(items, linkedItems, winner, getUrl, winnerUrl, platformPrefix))
        {
            return winnerUrl;
        }

        var consensusUrl = GetConsensusUrl(items, linkedItems, getUrl);
        if (consensusUrl != null && IsUrlAdoptable(items, linkedItems, winner, getUrl, consensusUrl, platformPrefix))
        {
            return consensusUrl;
        }

        foreach (var item in linkedItems.Where(x => !ReferenceEquals(x, winner)))
        {
            var candidate = getUrl(item);
            if (candidate != null &&
                IsUrlAdoptable(items, linkedItems, winner, getUrl, candidate, platformPrefix))
            {
                return candidate;
            }
        }

        return null;
    }

    private static Uri? GetConsensusUrl(
        IReadOnlyList<DiscoveryResult> items,
        IReadOnlyList<DiscoveryResult> linkedItems,
        Func<DiscoveryResult, Uri?> getUrl)
    {
        return items
            .Select(getUrl)
            .Where(url => url != null)
            .GroupBy(url => NormalizeUrl(url!))
            .Where(group => group.Count() >= 2)
            .OrderByDescending(group => group.Count())
            .Select(group => group.First())
            .FirstOrDefault(url => linkedItems.Any(item =>
            {
                var candidate = getUrl(item);
                return candidate != null && NormalizeUrl(candidate) == NormalizeUrl(url!);
            }));
    }

    private static bool IsUrlAdoptable(
        IReadOnlyList<DiscoveryResult> items,
        IReadOnlyList<DiscoveryResult> linkedItems,
        DiscoveryResult winner,
        Func<DiscoveryResult, Uri?> getUrl,
        Uri url,
        string platformPrefix)
    {
        var normalized = NormalizeUrl(url);
        var groupCount = items.Count(item =>
        {
            var candidate = getUrl(item);
            return candidate != null && NormalizeUrl(candidate) == normalized;
        });

        if (groupCount >= 2)
        {
            if (platformPrefix == "apple")
            {
                return linkedItems.Any(item =>
                    HasSameEpisodeIdentity(item, winner) &&
                    getUrl(item) is { } candidate &&
                    NormalizeUrl(candidate) == normalized);
            }

            return true;
        }

        if (platformPrefix == "apple")
        {
            if (groupCount >= 2)
            {
                return true;
            }

            return linkedItems.Any(item =>
                HasSameEpisodeIdentity(item, winner) &&
                getUrl(item) is { } candidate &&
                NormalizeUrl(candidate) == normalized);
        }

        return linkedItems.Any(item =>
        {
            var candidate = getUrl(item);
            return candidate != null && NormalizeUrl(candidate) == normalized;
        });
    }

    private static Func<DiscoveryResult, bool> HasSameEpisodeIdentityWith(DiscoveryResult winner) =>
        candidate => HasSameEpisodeIdentity(candidate, winner);

    private static bool HasSameEpisodeIdentity(DiscoveryResult left, DiscoveryResult right) =>
        NormalizeText(left.ShowName) == NormalizeText(right.ShowName) &&
        NormalizeText(left.EpisodeName) == NormalizeText(right.EpisodeName);

    private static string NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static bool SharesAnyUrlKey(DiscoveryResult left, DiscoveryResult right)
    {
        var rightKeys = GetUrlKeys(right).ToHashSet(StringComparer.Ordinal);
        return GetUrlKeys(left).Any(rightKeys.Contains);
    }

    private static DiscoveryResult SelectWinner(IReadOnlyList<DiscoveryResult> items)
    {
        var maxSameIdentityScore = items.Max(x => CountConfirmedUrlsForSameIdentity(items, x));
        var identityCandidates = items
            .Where(x => CountConfirmedUrlsForSameIdentity(items, x) == maxSameIdentityScore)
            .ToList();

        return identityCandidates
            .OrderByDescending(x => CountConfirmedUrls(items, x))
            .ThenByDescending(CountUrls)
            .ThenByDescending(CountSources)
            .ThenBy(x => x.Id)
            .First();
    }

    private static int CountSources(DiscoveryResult result) => result.Sources.Length;

    private static int CountConfirmedUrlsForSameIdentity(
        IReadOnlyList<DiscoveryResult> items,
        DiscoveryResult candidate)
    {
        var sameIdentityItems = items
            .Where(x => HasSameEpisodeIdentity(x, candidate))
            .ToList();

        return CountConfirmedUrls(sameIdentityItems, candidate);
    }

    private static int CountConfirmedUrls(IReadOnlyList<DiscoveryResult> items, DiscoveryResult candidate)
    {
        var count = 0;
        if (candidate.Urls.Spotify != null &&
            CountRowsWithUrl(items, x => x.Urls.Spotify, candidate.Urls.Spotify) >= 2)
        {
            count++;
        }

        if (candidate.Urls.Apple != null &&
            CountRowsWithUrl(items, x => x.Urls.Apple, candidate.Urls.Apple) >= 2)
        {
            count++;
        }

        if (candidate.Urls.YouTube != null &&
            CountRowsWithUrl(items, x => x.Urls.YouTube, candidate.Urls.YouTube) >= 2)
        {
            count++;
        }

        return count;
    }

    private static int CountRowsWithUrl(
        IReadOnlyList<DiscoveryResult> items,
        Func<DiscoveryResult, Uri?> getUrl,
        Uri url) =>
        items.Count(item =>
        {
            var candidate = getUrl(item);
            return candidate != null && NormalizeUrl(candidate) == NormalizeUrl(url);
        });

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

    private static string NormalizeUrl(Uri url)
    {
        var youTubeId = YouTubeIdResolver.Extract(url);
        if (youTubeId != null)
        {
            return youTubeId.ToLowerInvariant();
        }

        var appleEpisodeId = AppleIdResolver.GetEpisodeId(url);
        if (appleEpisodeId != null)
        {
            return appleEpisodeId.Value.ToString();
        }

        return url.GetLeftPart(UriPartial.Path).TrimEnd('/').ToLowerInvariant();
    }

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
