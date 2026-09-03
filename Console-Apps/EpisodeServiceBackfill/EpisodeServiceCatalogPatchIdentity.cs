using System.Text.Json;

namespace EpisodeServiceBackfill;

/// <summary>
/// Reads episode identity from the raw Cosmos JSON document and compares it to a catalog patch.
/// Patch targets must come from the same document that will be written.
/// </summary>
public static class EpisodeServiceCatalogPatchIdentity
{
    public static bool TryRead(string json, out Guid episodeId, out Guid podcastId)
    {
        episodeId = Guid.Empty;
        podcastId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        using var document = JsonDocument.Parse(json);
        return TryRead(document.RootElement, out episodeId, out podcastId);
    }

    public static bool TryRead(JsonElement root, out Guid episodeId, out Guid podcastId)
    {
        episodeId = Guid.Empty;
        podcastId = Guid.Empty;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!root.TryGetProperty("id", out var idEl) || !idEl.TryGetGuid(out episodeId) || episodeId == Guid.Empty)
        {
            episodeId = Guid.Empty;
            return false;
        }

        if (!root.TryGetProperty("podcastId", out var podcastEl) ||
            !podcastEl.TryGetGuid(out podcastId) ||
            podcastId == Guid.Empty)
        {
            episodeId = Guid.Empty;
            podcastId = Guid.Empty;
            return false;
        }

        return true;
    }

    public static bool Matches(string json, EpisodeServiceCatalogPatch patch, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(patch);
        reason = null;
        if (!TryRead(json, out var episodeId, out var podcastId))
        {
            reason = "missing or empty id/podcastId";
            return false;
        }

        if (patch.EpisodeId == Guid.Empty || patch.PodcastId == Guid.Empty)
        {
            reason = "empty patch identity";
            return false;
        }

        if (episodeId != patch.EpisodeId)
        {
            reason = "id mismatch";
            return false;
        }

        if (podcastId != patch.PodcastId)
        {
            reason = "podcastId mismatch";
            return false;
        }

        return true;
    }
}
