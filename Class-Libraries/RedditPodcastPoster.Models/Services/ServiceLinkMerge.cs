using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.YouTube;

namespace RedditPodcastPoster.Models.Services;

/// <summary>A service the source has a link for that the target already holds with a different link.</summary>
public sealed record ServiceLinkMergeConflict(string Service, Uri? TargetUrl, Uri? SourceUrl);

/// <summary>
/// The outcome of <see cref="ServiceLinkMerge.Plan"/>: the links to add to the target (services it has no link for),
/// the services both hold with the same link, and the services both hold with different links.
/// </summary>
public sealed record ServiceLinkMergePlan(
    IReadOnlyDictionary<string, ServiceLink> ToAdd,
    IReadOnlyList<string> AlreadyHeld,
    IReadOnlyList<ServiceLinkMergeConflict> Conflicts)
{
    public bool HasConflicts => Conflicts.Count > 0;
}

/// <summary>
/// Shared service-link merge for moving one catalogue item's platform links onto another item (RPP #1522 Move to an
/// existing film; meant for reuse by #1471 merge episodes and the #1416 split). Read-only: <see cref="Plan"/> decides,
/// <see cref="Apply"/> writes the planned additions onto a map.
/// <list type="bullet">
/// <item>A service the target has no usable link for takes the source link (url, image and language copied).</item>
/// <item>The same link on both is "already held" and changes nothing. YouTube links compare by video id, so
/// <c>youtu.be/x</c> and <c>youtube.com/watch?v=x</c> are the same; other services compare the url without a trailing
/// slash, case-insensitively.</item>
/// <item>Different links for one service are a conflict: an item holds one link per service. Until #1411 (alternate
/// YouTube links) a conflict means the move must stop and write nothing.</item>
/// </list>
/// Null entries and links with no url are ignored on both sides (#1508).
/// </summary>
public static class ServiceLinkMerge
{
    public static ServiceLinkMergePlan Plan(
        IReadOnlyDictionary<string, ServiceLink>? target,
        IReadOnlyDictionary<string, ServiceLink>? source)
    {
        var toAdd = new Dictionary<string, ServiceLink>(StringComparer.Ordinal);
        var held = new List<string>();
        var conflicts = new List<ServiceLinkMergeConflict>();
        if (source is null)
        {
            return new ServiceLinkMergePlan(toAdd, held, conflicts);
        }

        foreach (var (service, link) in source.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (link?.Url is null)
            {
                continue;
            }

            ServiceLink? existing = null;
            if (target is not null)
            {
                target.TryGetValue(service, out existing);
            }

            if (existing?.Url is null)
            {
                toAdd[service] = new ServiceLink { Url = link.Url, Image = link.Image, Language = link.Language };
            }
            else if (SameLink(service, existing.Url, link.Url))
            {
                held.Add(service);
            }
            else
            {
                conflicts.Add(new ServiceLinkMergeConflict(service, existing.Url, link.Url));
            }
        }

        return new ServiceLinkMergePlan(toAdd, held, conflicts);
    }

    /// <summary>Adds the plan's links to <paramref name="target"/> (created when null) and returns it.</summary>
    public static Dictionary<string, ServiceLink> Apply(
        Dictionary<string, ServiceLink>? target,
        ServiceLinkMergePlan plan)
    {
        var merged = ServiceLinkMap.CloneWithoutNulls(target) ?? new Dictionary<string, ServiceLink>();
        foreach (var (service, link) in plan.ToAdd)
        {
            merged[service] = new ServiceLink { Url = link.Url, Image = link.Image, Language = link.Language };
        }

        return merged;
    }

    /// <summary>True when the two urls are the same link for <paramref name="service"/>.</summary>
    public static bool SameLink(string service, Uri a, Uri b)
    {
        if (string.Equals(service, ServiceKeys.YouTube, StringComparison.Ordinal))
        {
            var idA = YouTubeIdResolver.Extract(a);
            var idB = YouTubeIdResolver.Extract(b);
            if (!string.IsNullOrWhiteSpace(idA) && !string.IsNullOrWhiteSpace(idB))
            {
                return string.Equals(idA, idB, StringComparison.Ordinal);
            }
        }

        return string.Equals(
            a.ToString().TrimEnd('/'),
            b.ToString().TrimEnd('/'),
            StringComparison.OrdinalIgnoreCase);
    }
}
