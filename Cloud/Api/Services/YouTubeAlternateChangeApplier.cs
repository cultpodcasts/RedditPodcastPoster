using Api.Models;
using Api.Services.Catalogue;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.YouTube;

namespace Api.Services;

/// <summary>
/// Shared <c>additionalYouTube</c> / <c>promoteYouTubeId</c> PATCH for films, news reports, TV episodes and
/// podcast episodes (RPP #1411 / #1526). Validation runs before anything is written. Film trailer-id rejection
/// stays in <see cref="Films.FilmLinkChangeApplier"/> via <paramref name="rejectId"/> on
/// <see cref="TryValidate"/>.
/// </summary>
internal static class YouTubeAlternateChangeApplier
{
    public static string StaleMessage(string itemLabel) =>
        $"The {itemLabel}'s additional YouTube links changed after this edit was loaded. Close and reopen the {itemLabel} to edit them.";

    public static bool TryValidate(
        IPlayable item,
        YouTubeAlternateChange[]? additional,
        string? promoteYouTubeId,
        string? resultingPrimary,
        string itemLabel,
        Func<string, string?>? rejectId,
        out string? error)
    {
        error = null;
        var promote = string.IsNullOrWhiteSpace(promoteYouTubeId) ? null : promoteYouTubeId.Trim();
        HashSet<string>? resultingIds = null;

        if (additional is not null)
        {
            resultingIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in additional)
            {
                if (!TryResolveChange(item, entry, itemLabel, out var youTubeId, out error))
                {
                    return false;
                }

                if (!resultingIds.Add(youTubeId))
                {
                    error = $"additional YouTube '{youTubeId}' is listed twice";
                    return false;
                }

                if (string.Equals(youTubeId, resultingPrimary, StringComparison.Ordinal))
                {
                    error = $"additional YouTube '{youTubeId}' is already the {itemLabel}'s primary YouTube link";
                    return false;
                }

                if (rejectId?.Invoke(youTubeId) is { } rejected)
                {
                    error = rejected;
                    return false;
                }
            }
        }

        if (promote is not null)
        {
            if (resultingIds is not null)
            {
                if (!resultingIds.Contains(promote))
                {
                    error = $"promoteYouTubeId '{promote}' is not in additionalYouTube";
                    return false;
                }
            }
            else if (!YouTubeAlternates.Holds(item.AdditionalYouTube, promote))
            {
                error = $"the {itemLabel} has no additional YouTube '{promote}'";
                return false;
            }
        }

        return true;
    }

    public static void Apply(IPlayable item, YouTubeAlternateChange[]? additional, string? promoteYouTubeId)
    {
        if (additional is not null)
        {
            ApplyList(item, additional);
        }

        if (!string.IsNullOrWhiteSpace(promoteYouTubeId))
        {
            Promote(item, promoteYouTubeId.Trim(), DateTime.UtcNow);
        }
    }

    /// <summary>
    /// The primary YouTube id after a <c>services</c> patch is applied in memory. When
    /// <paramref name="services"/> is null, the id already on <paramref name="item"/>. A services patch that
    /// removes YouTube yields null, so validation uses the post-PATCH primary, not the pre-PATCH one.
    /// </summary>
    public static string? ResultingPrimaryYouTubeId(
        IPlayable item,
        Dictionary<string, ServiceLink>? services)
    {
        if (services is null)
        {
            return CurrentPrimaryYouTubeId(item);
        }

        var applied = PlayableServicesChangeApplier.Apply(item.Services, services);
        return ExtractFromServices(applied);
    }

    public static string? CurrentPrimaryYouTubeId(IPlayable item)
    {
        if (item is Episode episode)
        {
            return EpisodeServicePresence.YouTubeEpisodeId(episode);
        }

        if (item is ICatalogueYouTubeItem catalogue && !string.IsNullOrWhiteSpace(catalogue.YouTubeId))
        {
            return catalogue.YouTubeId;
        }

        return ExtractFromServices(item.Services);
    }

    /// <summary>YouTube ids in <paramref name="additional"/> that this item does not already hold and that are not the resulting primary.</summary>
    public static IReadOnlyList<string> NewIds(
        IPlayable item,
        YouTubeAlternateChange[]? additional,
        string? resultingPrimary)
    {
        if (additional is null)
        {
            return [];
        }

        var ids = new List<string>();
        foreach (var entry in additional)
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

            if (YouTubeAlternates.Holds(item.AdditionalYouTube, id))
            {
                continue;
            }

            ids.Add(id);
        }

        return ids.Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Null <paramref name="expectedIds"/> and an empty array both mean the client listed no ids.</summary>
    public static string[] IdsTheClientDidNotList(
        IReadOnlyList<YouTubeAlternate>? stored,
        string[]? expectedIds)
    {
        var expected = expectedIds is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : expectedIds.ToHashSet(StringComparer.Ordinal);
        return stored?
                   .Select(item => item.YouTubeId)
                   .Where(id => !expected.Contains(id))
                   .ToArray()
               ?? [];
    }

    private static bool TryResolveChange(
        IPlayable item,
        YouTubeAlternateChange entry,
        string itemLabel,
        out string youTubeId,
        out string? error)
    {
        youTubeId = string.Empty;
        error = null;
        var listedId = string.IsNullOrWhiteSpace(entry.YouTubeId) ? null : entry.YouTubeId.Trim();
        if (listedId is not null)
        {
            if (YouTubeAlternates.Holds(item.AdditionalYouTube, listedId))
            {
                youTubeId = listedId;
                return true;
            }

            if (string.IsNullOrWhiteSpace(entry.Url))
            {
                error = $"the {itemLabel} has no additional YouTube '{listedId}'";
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(entry.Url) || !Uri.TryCreate(entry.Url, UriKind.Absolute, out var url) ||
            (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp))
        {
            error = "additional YouTube url must be an absolute http(s) YouTube URL";
            return false;
        }

        var extracted = YouTubeIdResolver.Extract(url);
        if (string.IsNullOrWhiteSpace(extracted))
        {
            error = "additional YouTube url must be a YouTube video URL";
            return false;
        }

        if (listedId is not null && !string.Equals(listedId, extracted, StringComparison.Ordinal))
        {
            error = $"additional YouTube youTubeId '{listedId}' does not match url";
            return false;
        }

        youTubeId = extracted;
        return true;
    }

    private static void ApplyList(IPlayable item, YouTubeAlternateChange[] keep)
    {
        var current = item.AdditionalYouTube ?? [];
        var kept = new List<YouTubeAlternate>(keep.Length);
        var now = DateTime.UtcNow;
        foreach (var change in keep)
        {
            var existingId = string.IsNullOrWhiteSpace(change.YouTubeId) ? null : change.YouTubeId.Trim();
            var existing = existingId is null
                ? null
                : current.FirstOrDefault(alternate =>
                    string.Equals(alternate.YouTubeId, existingId, StringComparison.Ordinal));
            if (existing is not null)
            {
                if (!string.IsNullOrWhiteSpace(change.Title))
                {
                    existing.Title = change.Title.Trim();
                }

                if (!string.IsNullOrWhiteSpace(change.ChannelName))
                {
                    existing.ChannelName = change.ChannelName.Trim();
                }

                kept.Add(existing);
                continue;
            }

            if (!Uri.TryCreate(change.Url, UriKind.Absolute, out var url))
            {
                continue;
            }

            var created = YouTubeAlternates.FromUrl(url, now);
            if (created is null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(change.Title))
            {
                created.Title = change.Title.Trim();
            }

            if (!string.IsNullOrWhiteSpace(change.ChannelName))
            {
                created.ChannelName = change.ChannelName.Trim();
            }

            kept.Add(created);
        }

        item.AdditionalYouTube = YouTubeAlternates.OrNull(kept);
    }

    private static void Promote(IPlayable item, string youTubeId, DateTime nowUtc)
    {
        var current = item.AdditionalYouTube;
        var promoted = current?.FirstOrDefault(alternate =>
            string.Equals(alternate.YouTubeId, youTubeId, StringComparison.Ordinal));
        if (promoted is null)
        {
            return;
        }

        var previous = YouTubeAlternates.FromPrimary(item.Services, CurrentPrimaryYouTubeId(item), nowUtc);
        item.Services ??= new Dictionary<string, ServiceLink>();
        item.Services[ServiceKeys.YouTube] = new ServiceLink
        {
            Url = promoted.Url,
            Image = promoted.Image
        };
        SyncPrimary(item);

        var remaining = (current ?? [])
            .Where(alternate => !string.Equals(alternate.YouTubeId, youTubeId, StringComparison.Ordinal))
            .ToList();
        if (previous is not null && !YouTubeAlternates.Holds(remaining, previous.YouTubeId))
        {
            remaining.Insert(0, previous);
        }

        item.AdditionalYouTube = YouTubeAlternates.OrNull(remaining);
    }

    /// <summary>
    /// Catalogue kinds sync compact <c>youTubeId</c> from the URL. Podcast episodes write <c>ids.youtube</c>
    /// the same way a services YouTube edit does.
    /// </summary>
    private static void SyncPrimary(IPlayable item)
    {
        if (item is Episode episode)
        {
            EpisodeServicePresence.SetYouTubeIdentity(episode, ExtractFromServices(episode.Services));
            return;
        }

        if (item is ICatalogueYouTubeItem catalogue)
        {
            EpisodeServicePresence.SyncCatalogueYouTubeIdFromUrl(catalogue);
        }
    }

    private static string? ExtractFromServices(IReadOnlyDictionary<string, ServiceLink>? services)
    {
        if (services is null ||
            !services.TryGetValue(ServiceKeys.YouTube, out var link) ||
            link?.Url is null)
        {
            return null;
        }

        return YouTubeIdResolver.Extract(link.Url);
    }
}
