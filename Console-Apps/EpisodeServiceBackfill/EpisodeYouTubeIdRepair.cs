using System.Globalization;
using System.Text;
using System.Text.Json;
using RedditPodcastPoster.Models.YouTube;

namespace EpisodeServiceBackfill;

/// <summary>
/// A podcast-episode document with <c>services.youtube.url</c> but no nested <c>ids.youtube</c>, and the
/// single patch that fixes it. <see cref="Url"/> is the raw stored string (not <see cref="Uri.ToString"/>),
/// because the patch condition compares it with the stored value.
/// </summary>
public sealed record EpisodeYouTubeIdRepairPlan(
    Guid EpisodeId,
    Guid PodcastId,
    string Url,
    string YouTubeId,
    bool IdsObjectExists,
    long Ts,
    bool Removed)
{
    /// <summary>Cosmos patch path: the id field alone, or <c>/ids</c> when the document has no ids object.</summary>
    public string PatchPath => IdsObjectExists ? EpisodeYouTubeIdRepair.YouTubeIdPath : EpisodeYouTubeIdRepair.IdsPath;
}

/// <summary>A stored <c>ids.youtube</c> that is not the video in <c>services.youtube.url</c>. Reported, never patched.</summary>
public sealed record EpisodeYouTubeIdMismatch(
    Guid EpisodeId,
    Guid PodcastId,
    string StoredYouTubeId,
    string UrlYouTubeId,
    string Url);

/// <summary>
/// <c>--youtube-id-only</c>: derive <c>ids.youtube</c> from the video id in <c>services.youtube.url</c>.
/// Pure planning over raw Cosmos JSON so it does not depend on the <c>Episode</c> serializer; the runner
/// writes one patch per document with a per-plan filter predicate, so re-running is a no-op.
/// </summary>
public static class EpisodeYouTubeIdRepair
{
    public const string YouTubeIdPath = "/ids/youtube";
    public const string IdsPath = "/ids";

    /// <summary>Documents whose stored YouTube id is missing while the YouTube url is present.</summary>
    public const string CandidateQuery =
        "SELECT * FROM c WHERE IS_DEFINED(c.services.youtube.url) AND NOT IS_NULL(c.services.youtube.url) " +
        "AND (NOT IS_DEFINED(c.ids.youtube) OR IS_NULL(c.ids.youtube) OR c.ids.youtube = '')";

    /// <summary>Documents with both a stored id and a url, for the read-only mismatch report.</summary>
    public const string StoredIdWithUrlQuery =
        "SELECT c.id, c.podcastId, c.ids, c.services FROM c WHERE IS_STRING(c.ids.youtube) AND c.ids.youtube != '' " +
        "AND IS_STRING(c.services.youtube.url)";

    private const string YouTubeIdMissing =
        "(NOT IS_DEFINED(c.ids.youtube) OR IS_NULL(c.ids.youtube) OR c.ids.youtube = '')";

    private const string IdsObjectMissing = "(NOT IS_DEFINED(c.ids) OR IS_NULL(c.ids))";

    public enum SkipReason
    {
        None,
        NotJson,
        MissingIdentity,
        IdAlreadySet,
        NoYouTubeUrl,
        UrlHasNoVideoId
    }

    /// <summary>
    /// The condition the patch is applied under, for this plan only:
    /// <list type="bullet">
    /// <item><c>/ids/youtube</c>: the stored id is still missing.</item>
    /// <item><c>/ids</c>: the document still has no ids object, so the whole-object set cannot replace
    /// Spotify/Apple ids written after the scan.</item>
    /// </list>
    /// Both also require <c>services.youtube.url</c> to still be the url seen at scan time, so a curator
    /// clear or a change of video between the scan and the apply is never given the old video's id.
    /// </summary>
    public static string PatchFilterPredicateFor(EpisodeYouTubeIdRepairPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var idClause = plan.IdsObjectExists ? YouTubeIdMissing : IdsObjectMissing;
        return $"FROM c WHERE {idClause} AND c.services.youtube.url = {SqlStringLiteral(plan.Url)}";
    }

    /// <summary>Cosmos SQL single-quoted string literal with backslash escapes.</summary>
    public static string SqlStringLiteral(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('\'');
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '\\':
                    builder.Append(@"\\");
                    break;
                case '\'':
                    builder.Append(@"\'");
                    break;
                default:
                    if (char.IsControl(ch))
                    {
                        builder.Append(@"\u").Append(((int) ch).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(ch);
                    }

                    break;
            }
        }

        builder.Append('\'');
        return builder.ToString();
    }

    public static EpisodeYouTubeIdRepairPlan? Plan(string json, out SkipReason reason)
    {
        if (!TryParse(json, out var document))
        {
            reason = SkipReason.NotJson;
            return null;
        }

        using (document)
        {
            var root = document.RootElement;
            if (!TryGetIdentity(root, out var episodeId, out var podcastId))
            {
                reason = SkipReason.MissingIdentity;
                return null;
            }

            var idsObjectExists = root.TryGetProperty("ids", out var ids) && ids.ValueKind == JsonValueKind.Object;
            if (TryGetStoredYouTubeId(root, out _))
            {
                reason = SkipReason.IdAlreadySet;
                return null;
            }

            if (!TryGetYouTubeUrl(root, out var rawUrl, out var url))
            {
                reason = SkipReason.NoYouTubeUrl;
                return null;
            }

            var youTubeId = YouTubeIdResolver.Extract(url);
            if (string.IsNullOrWhiteSpace(youTubeId))
            {
                reason = SkipReason.UrlHasNoVideoId;
                return null;
            }

            var ts = root.TryGetProperty("_ts", out var tsElement) && tsElement.TryGetInt64(out var t) ? t : 0;
            var removed = root.TryGetProperty("removed", out var removedElement) &&
                          removedElement.ValueKind == JsonValueKind.True;
            reason = SkipReason.None;
            return new EpisodeYouTubeIdRepairPlan(
                episodeId, podcastId, rawUrl, youTubeId, idsObjectExists, ts, removed);
        }
    }

    /// <summary>A stored id that differs from the url's video id, or null when they agree or cannot be compared.</summary>
    public static EpisodeYouTubeIdMismatch? FindMismatch(string json)
    {
        if (!TryParse(json, out var document))
        {
            return null;
        }

        using (document)
        {
            var root = document.RootElement;
            if (!TryGetIdentity(root, out var episodeId, out var podcastId) ||
                !TryGetStoredYouTubeId(root, out var storedId) ||
                !TryGetYouTubeUrl(root, out var rawUrl, out var url))
            {
                return null;
            }

            var urlId = YouTubeIdResolver.Extract(url);
            return string.IsNullOrWhiteSpace(urlId) || string.Equals(urlId, storedId, StringComparison.Ordinal)
                ? null
                : new EpisodeYouTubeIdMismatch(episodeId, podcastId, storedId, urlId, rawUrl);
        }
    }

    /// <summary>The value written at <see cref="EpisodeYouTubeIdRepairPlan.PatchPath"/>.</summary>
    public static object PatchValue(EpisodeYouTubeIdRepairPlan plan) =>
        plan.IdsObjectExists
            ? plan.YouTubeId
            : new Dictionary<string, string> { ["youtube"] = plan.YouTubeId };

    private static bool TryParse(string json, out JsonDocument document)
    {
        try
        {
            document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                return true;
            }

            document.Dispose();
        }
        catch (JsonException)
        {
        }

        document = null!;
        return false;
    }

    private static bool TryGetIdentity(JsonElement root, out Guid episodeId, out Guid podcastId)
    {
        podcastId = Guid.Empty;
        return TryGetGuid(root, "id", out episodeId) && TryGetGuid(root, "podcastId", out podcastId);
    }

    private static bool TryGetStoredYouTubeId(JsonElement root, out string storedId)
    {
        storedId = string.Empty;
        if (root.TryGetProperty("ids", out var ids) &&
            ids.ValueKind == JsonValueKind.Object &&
            ids.TryGetProperty("youtube", out var existing) &&
            existing.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(existing.GetString()))
        {
            storedId = existing.GetString()!;
            return true;
        }

        return false;
    }

    private static bool TryGetYouTubeUrl(JsonElement root, out string rawUrl, out Uri url)
    {
        rawUrl = string.Empty;
        url = null!;
        if (!root.TryGetProperty("services", out var services) ||
            services.ValueKind != JsonValueKind.Object ||
            !services.TryGetProperty("youtube", out var youtube) ||
            youtube.ValueKind != JsonValueKind.Object ||
            !youtube.TryGetProperty("url", out var urlElement) ||
            urlElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        rawUrl = urlElement.GetString()!;
        return Uri.TryCreate(rawUrl, UriKind.Absolute, out url!);
    }

    private static bool TryGetGuid(JsonElement root, string name, out Guid value)
    {
        value = Guid.Empty;
        return root.TryGetProperty(name, out var element) &&
               element.ValueKind == JsonValueKind.String &&
               Guid.TryParse(element.GetString(), out value) &&
               value != Guid.Empty;
    }
}
