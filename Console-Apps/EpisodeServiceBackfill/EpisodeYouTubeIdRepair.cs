using System.Text.Json;
using RedditPodcastPoster.Models.YouTube;

namespace EpisodeServiceBackfill;

/// <summary>
/// A podcast-episode document with <c>services.youtube.url</c> but no nested <c>ids.youtube</c>, and the
/// single patch that fixes it.
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

/// <summary>
/// <c>--youtube-id-only</c>: derive <c>ids.youtube</c> from the video id in <c>services.youtube.url</c>.
/// Pure planning over raw Cosmos JSON so it does not depend on the <c>Episode</c> serializer; the runner
/// writes one patch per document with a filter predicate, so re-running is a no-op.
/// </summary>
public static class EpisodeYouTubeIdRepair
{
    public const string YouTubeIdPath = "/ids/youtube";
    public const string IdsPath = "/ids";

    /// <summary>Documents whose stored YouTube id is missing while the YouTube url is present.</summary>
    public const string CandidateQuery =
        "SELECT * FROM c WHERE IS_DEFINED(c.services.youtube.url) AND NOT IS_NULL(c.services.youtube.url) " +
        "AND (NOT IS_DEFINED(c.ids.youtube) OR IS_NULL(c.ids.youtube) OR c.ids.youtube = '')";

    /// <summary>Patch only applies while the id is still missing (idempotent; never overwrites an id).</summary>
    public const string PatchFilterPredicate =
        "FROM c WHERE NOT IS_DEFINED(c.ids.youtube) OR IS_NULL(c.ids.youtube) OR c.ids.youtube = ''";

    public enum SkipReason
    {
        None,
        NotJson,
        MissingIdentity,
        IdAlreadySet,
        NoYouTubeUrl,
        UrlHasNoVideoId
    }

    public static EpisodeYouTubeIdRepairPlan? Plan(string json, out SkipReason reason)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            reason = SkipReason.NotJson;
            return null;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !TryGetGuid(root, "id", out var episodeId) ||
                !TryGetGuid(root, "podcastId", out var podcastId))
            {
                reason = SkipReason.MissingIdentity;
                return null;
            }

            var idsObjectExists = root.TryGetProperty("ids", out var ids) && ids.ValueKind == JsonValueKind.Object;
            if (idsObjectExists &&
                ids.TryGetProperty("youtube", out var existing) &&
                existing.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(existing.GetString()))
            {
                reason = SkipReason.IdAlreadySet;
                return null;
            }

            if (!root.TryGetProperty("services", out var services) ||
                services.ValueKind != JsonValueKind.Object ||
                !services.TryGetProperty("youtube", out var youtube) ||
                youtube.ValueKind != JsonValueKind.Object ||
                !youtube.TryGetProperty("url", out var urlElement) ||
                urlElement.ValueKind != JsonValueKind.String ||
                !Uri.TryCreate(urlElement.GetString(), UriKind.Absolute, out var url))
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
                episodeId, podcastId, url.ToString(), youTubeId, idsObjectExists, ts, removed);
        }
    }

    /// <summary>The value written at <see cref="EpisodeYouTubeIdRepairPlan.PatchPath"/>.</summary>
    public static object PatchValue(EpisodeYouTubeIdRepairPlan plan) =>
        plan.IdsObjectExists
            ? plan.YouTubeId
            : new Dictionary<string, string> { ["youtube"] = plan.YouTubeId };

    private static bool TryGetGuid(JsonElement root, string name, out Guid value)
    {
        value = Guid.Empty;
        return root.TryGetProperty(name, out var element) &&
               element.ValueKind == JsonValueKind.String &&
               Guid.TryParse(element.GetString(), out value) &&
               value != Guid.Empty;
    }
}
