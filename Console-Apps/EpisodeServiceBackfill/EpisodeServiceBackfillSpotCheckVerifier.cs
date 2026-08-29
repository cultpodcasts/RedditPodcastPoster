using System.Text.Json;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
namespace EpisodeServiceBackfill;

public sealed record EpisodeServiceBackfillSpotCheckFailure(Guid EpisodeId, Guid PodcastId, string Reason);

public sealed record EpisodeServiceBackfillSpotCheckReport(
    int Sampled,
    int Checked,
    int Ok,
    int Mismatch,
    int Missing,
    IReadOnlyList<EpisodeServiceBackfillSpotCheckFailure> Failures);

public static class EpisodeServiceBackfillSpotCheckVerifier
{
    public static EpisodeServiceBackfillSpotCheckReport Verify(
        IReadOnlyList<EpisodeServiceBackfillSpotCheckSample> samples,
        IReadOnlyDictionary<Guid, string> storedJsonByEpisodeId,
        bool applied)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(storedJsonByEpisodeId);

        var ok = 0;
        var mismatch = 0;
        var missing = 0;
        var failures = new List<EpisodeServiceBackfillSpotCheckFailure>();

        foreach (var sample in samples)
        {
            if (!storedJsonByEpisodeId.TryGetValue(sample.EpisodeId, out var json) ||
                string.IsNullOrWhiteSpace(json))
            {
                missing++;
                failures.Add(new EpisodeServiceBackfillSpotCheckFailure(
                    sample.EpisodeId, sample.PodcastId, "not found"));
                continue;
            }

            var reason = VerifyOne(sample, json, applied);
            if (reason is null)
            {
                ok++;
                continue;
            }

            mismatch++;
            failures.Add(new EpisodeServiceBackfillSpotCheckFailure(
                sample.EpisodeId, sample.PodcastId, reason));
        }

        return new EpisodeServiceBackfillSpotCheckReport(
            samples.Count,
            samples.Count,
            ok,
            mismatch,
            missing,
            failures);
    }

    public static string? VerifyOne(
        EpisodeServiceBackfillSpotCheckSample sample,
        string storedJson,
        bool applied)
    {
        ArgumentNullException.ThrowIfNull(sample);
        using var document = JsonDocument.Parse(storedJson);
        var root = document.RootElement;
        if (!EpisodeServiceCatalogPatchIdentity.TryRead(root, out var episodeId, out var podcastId))
        {
            return "id mismatch";
        }

        if (episodeId != sample.EpisodeId || podcastId != sample.PodcastId)
        {
            return "id mismatch";
        }

        var needsBackfill = EpisodeServiceDocumentMigration.NeedsBackfill(root);
        if (applied)
        {
            if (needsBackfill)
            {
                return "still NeedsBackfill";
            }

            if (!ServicesMatch(root, sample.Services))
            {
                return "services/ids differ";
            }

            if (!IdsMatch(root, sample.Ids))
            {
                return "services/ids differ";
            }
        }

        return null;
    }

    private static bool ServicesMatch(JsonElement root, Dictionary<string, EpisodeServiceLink>? expected)
    {
        Dictionary<string, EpisodeServiceLink>? stored = null;
        if (root.TryGetProperty("services", out var servicesEl) && servicesEl.ValueKind == JsonValueKind.Object)
        {
            stored = JsonSerializer.Deserialize<Dictionary<string, EpisodeServiceLink>>(
                servicesEl.GetRawText(), EpisodeDocumentJsonOptions.Instance);
        }

        if (expected is not { Count: > 0 })
        {
            return stored is not { Count: > 0 };
        }

        if (stored is null || stored.Count != expected.Count)
        {
            return false;
        }

        foreach (var (key, link) in expected)
        {
            if (!stored.TryGetValue(key, out var storedLink))
            {
                return false;
            }

            if (storedLink.Url != link.Url || storedLink.Image != link.Image)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IdsMatch(JsonElement root, EpisodeIds? expected)
    {
        EpisodeIds? stored = null;
        if (root.TryGetProperty("ids", out var idsEl) && idsEl.ValueKind == JsonValueKind.Object)
        {
            stored = JsonSerializer.Deserialize<EpisodeIds>(idsEl.GetRawText(), EpisodeDocumentJsonOptions.Instance);
        }

        if (expected is null || expected.IsEmpty)
        {
            return stored is null || stored.IsEmpty;
        }

        if (stored is null)
        {
            return false;
        }

        return stored.Spotify == expected.Spotify &&
               stored.Apple == expected.Apple &&
               stored.YouTube == expected.YouTube;
    }
}
