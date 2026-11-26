using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.YouTube;

namespace RedditPodcastPoster.Models.Services;

/// <summary>A service the source has a link for that the target already holds with a different link.</summary>
public sealed record ServiceLinkMergeConflict(string Service, Uri? TargetUrl, Uri? SourceUrl);

/// <summary>
/// The outcome of <see cref="ServiceLinkMerge.Plan"/>: the links to add to the target (services it has no link for),
/// the services both hold with the same link, YouTube uploads to add as alternates (the target already has a different
/// primary YouTube), and the services both hold with different links (Spotify, Apple, and other single-valued services).
/// </summary>
public sealed record ServiceLinkMergePlan(
    IReadOnlyDictionary<string, ServiceLink> ToAdd,
    IReadOnlyList<string> AlreadyHeld,
    IReadOnlyList<ServiceLink> YouTubeAlternatesToAdd,
    IReadOnlyList<ServiceLinkMergeConflict> Conflicts)
{
    public bool HasConflicts => Conflicts.Count > 0;

    /// <summary>True when the plan would change the target's services or its additional YouTube list.</summary>
    public bool HasWork => ToAdd.Count > 0 || YouTubeAlternatesToAdd.Count > 0;
}

/// <summary>
/// Shared service-link merge for moving one catalogue item's platform links onto another item (RPP #1522 Move to an
/// existing film; meant for reuse by #1471 merge episodes and the #1416 split). Read-only: <see cref="Plan"/> decides,
/// <see cref="Apply"/> writes the planned service-link additions onto a map, and <see cref="ApplyYouTubeAlternates"/>
/// appends planned YouTube alternates.
/// <list type="bullet">
/// <item>A service the target has no usable link for takes the source link (url, image and language copied).</item>
/// <item>The same link on both is "already held" and changes nothing. YouTube links compare by video id, so
/// <c>youtu.be/x</c> and <c>youtube.com/watch?v=x</c> are the same; other services compare the url without a trailing
/// slash, case-insensitively. A source YouTube that is already an alternate on the target is also already held.</item>
/// <item>A different YouTube upload (RPP #1411) is added as an alternate: the primary <c>services.youtube</c> is
/// unchanged. Spotify, Apple and other services still conflict: an item holds one link per those services, and a
/// conflict means the move must stop and write nothing.</item>
/// </list>
/// Null entries and links with no url are ignored on both sides (#1508).
/// </summary>
public static class ServiceLinkMerge
{
    public static ServiceLinkMergePlan Plan(
        IReadOnlyDictionary<string, ServiceLink>? target,
        IReadOnlyDictionary<string, ServiceLink>? source,
        IReadOnlyList<YouTubeAlternate>? targetYouTubeAlternates = null)
    {
        var toAdd = new Dictionary<string, ServiceLink>(StringComparer.Ordinal);
        var held = new List<string>();
        var youTubeAlternates = new List<ServiceLink>();
        var conflicts = new List<ServiceLinkMergeConflict>();
        if (source is null)
        {
            return new ServiceLinkMergePlan(toAdd, held, youTubeAlternates, conflicts);
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
            else if (string.Equals(service, ServiceKeys.YouTube, StringComparison.Ordinal))
            {
                if (YouTubeAlternates.HoldsUrl(targetYouTubeAlternates, link.Url))
                {
                    held.Add(service);
                }
                else
                {
                    youTubeAlternates.Add(new ServiceLink
                    {
                        Url = link.Url,
                        Image = link.Image,
                        Language = link.Language
                    });
                }
            }
            else
            {
                conflicts.Add(new ServiceLinkMergeConflict(service, existing.Url, link.Url));
            }
        }

        return new ServiceLinkMergePlan(toAdd, held, youTubeAlternates, conflicts);
    }

    /// <summary>Adds the plan's service links to <paramref name="target"/> (created when null) and returns it.</summary>
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

    /// <summary>
    /// Appends the plan's YouTube alternates to <paramref name="current"/> (by video id; already-held ids are
    /// skipped). <paramref name="fromEpisode"/> supplies title, channel, release and the source episode id when
    /// present; otherwise each alternate is stored from its url alone.
    /// </summary>
    public static List<YouTubeAlternate>? ApplyYouTubeAlternates(
        IList<YouTubeAlternate>? current,
        ServiceLinkMergePlan plan,
        DateTime addedAtUtc,
        Episode? fromEpisode = null,
        string? channelId = null,
        string? channelName = null)
    {
        if (plan.YouTubeAlternatesToAdd.Count == 0)
        {
            return YouTubeAlternates.OrNull(current);
        }

        var list = current?.ToList() ?? [];
        foreach (var link in plan.YouTubeAlternatesToAdd)
        {
            YouTubeAlternate? alternate = null;
            if (fromEpisode is not null)
            {
                alternate = YouTubeAlternates.FromEpisode(fromEpisode, addedAtUtc, channelId, channelName);
                if (alternate is not null &&
                    (link.Url is null ||
                     !string.Equals(alternate.YouTubeId, YouTubeIdResolver.Extract(link.Url), StringComparison.Ordinal)))
                {
                    alternate = null;
                }
            }

            alternate ??= link.Url is null ? null : YouTubeAlternates.FromUrl(link.Url, addedAtUtc);
            if (alternate is not null && !string.IsNullOrWhiteSpace(link.Image?.ToString()))
            {
                alternate.Image ??= link.Image;
            }

            if (alternate is not null)
            {
                list = YouTubeAlternates.Add(list, alternate);
            }
        }

        return YouTubeAlternates.OrNull(list);
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
