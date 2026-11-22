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
/// Looks up the holder with <see cref="IUrlMembershipLookup.FindCatalogueServiceUrlOwner"/> (films, TV,
/// news, and podcast episodes, including additionalYouTube) and excludes the source episode being
/// moved and the target itself.
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
        var url = new Uri($"https://www.youtube.com/watch?v={youTubeId}");
        var owner = await lookup.FindCatalogueServiceUrlOwner(url, cancellationToken);
        if (owner is null)
        {
            return null;
        }

        var except = exceptIds as ISet<Guid> ?? exceptIds.ToHashSet();
        if (owner.PlayableId is Guid id && except.Contains(id))
        {
            return null;
        }

        return owner;
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
