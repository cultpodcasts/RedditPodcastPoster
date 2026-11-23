using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.YouTube;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Services;
using Api.Models;
using Api.Services.Films;
using YouTubeIdResolver = RedditPodcastPoster.Models.YouTube.YouTubeIdResolver;

namespace Api.Services;

/// <summary>
/// Cross-item uniqueness for YouTube ids (RPP #1411): a YouTube id may belong to only one playable.
/// Looks up the holder with <see cref="IUrlMembershipLookup.FindYouTubeHolderExcluding"/>, which
/// puts source/target exclusions in the queries and includes podcast-episode primaries.
/// </summary>
internal static class YouTubeAlternateOwnership
{
    public static string ConflictMessage(string youTubeId, CatalogueServiceUrlOwner owner)
    {
        var kind = owner.ContentKind;
        var id = owner.PlayableId;
        if (!string.IsNullOrWhiteSpace(owner.ParentName))
        {
            return $"YouTube '{youTubeId}' is already on {kind} '{owner.ParentName}' ({id}).";
        }

        return $"YouTube '{youTubeId}' is already on {kind} {id}.";
    }

    public static async Task<CatalogueServiceUrlOwner?> FindOtherOwner(
        IUrlMembershipLookup lookup,
        string youTubeId,
        IEnumerable<Guid> exceptIds,
        CancellationToken cancellationToken)
    {
        var except = exceptIds as IReadOnlyCollection<Guid> ?? exceptIds.ToHashSet();
        return await lookup.FindYouTubeHolderExcluding(youTubeId, except, cancellationToken);
    }

    public static IReadOnlyList<string> IdsAMergeWouldAdd(
        IPlayable target,
        IReadOnlyDictionary<string, ServiceLink> source)
    {
        var plan = ServiceLinkMerge.Plan(target.Services, source, target.AdditionalYouTube);
        var ids = new List<string>();
        foreach (var link in plan.YouTubeAlternatesToAdd)
        {
            if (link.Url is not null)
            {
                var id = YouTubeIdResolver.Extract(link.Url);
                if (!string.IsNullOrWhiteSpace(id))
                {
                    ids.Add(id);
                }
            }
        }

        if (plan.ToAdd.TryGetValue(ServiceKeys.YouTube, out var primary) && primary.Url is not null)
        {
            var id = YouTubeIdResolver.Extract(primary.Url);
            if (!string.IsNullOrWhiteSpace(id))
            {
                ids.Add(id);
            }
        }

        return ids.Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// YouTube ids a merge or attach is about, including a source id the target already holds, so a
    /// second holder is still found when the target is excluded from the uniqueness query.
    /// </summary>
    public static IReadOnlyList<string> YouTubeIdsToCheckForMerge(
        IPlayable target,
        IReadOnlyDictionary<string, ServiceLink> source)
    {
        var ids = IdsAMergeWouldAdd(target, source).ToList();
        if (source.TryGetValue(ServiceKeys.YouTube, out var primary) && primary.Url is not null)
        {
            var id = YouTubeIdResolver.Extract(primary.Url);
            if (!string.IsNullOrWhiteSpace(id))
            {
                ids.Add(id);
            }
        }

        return ids.Distinct(StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> NewFilmAlternateIds(Film film, FilmChangeRequest change)
    {
        if (change.AdditionalYouTube is null)
        {
            return [];
        }

        var resultingPrimary = FilmLinkChangeApplier.ResultingPrimaryYouTubeId(film, change);
        var ids = new List<string>();
        foreach (var entry in change.AdditionalYouTube)
        {
            var id = entry.YouTubeId;
            if (string.IsNullOrWhiteSpace(id) &&
                Uri.TryCreate(entry.Url, UriKind.Absolute, out var url))
            {
                id = YouTubeIdResolver.Extract(url);
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (string.Equals(id, resultingPrimary, StringComparison.Ordinal))
            {
                continue;
            }

            if (YouTubeAlternates.Holds(film.AdditionalYouTube, id))
            {
                continue;
            }

            ids.Add(id);
        }

        return ids.Distinct(StringComparer.Ordinal).ToArray();
    }

    public static async Task<string?> ConflictMessageIfHeld(
        IUrlMembershipLookup lookup,
        IReadOnlyList<string> youTubeIds,
        IEnumerable<Guid> exceptIds,
        CancellationToken cancellationToken)
    {
        var except = exceptIds.ToHashSet();
        foreach (var youTubeId in youTubeIds)
        {
            var owner = await FindOtherOwner(lookup, youTubeId, except, cancellationToken);
            if (owner is not null)
            {
                return ConflictMessage(youTubeId, owner);
            }
        }

        return null;
    }
}
