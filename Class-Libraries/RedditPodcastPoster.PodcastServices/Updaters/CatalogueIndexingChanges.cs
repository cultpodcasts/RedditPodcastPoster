using System.Text.Json;
using System.Text.Json.Nodes;
using RedditPodcastPoster.Models.Catalogue;

namespace RedditPodcastPoster.PodcastServices.Updaters;

/// <summary>
/// Which <c>indexing</c> members an index pass changed, by JSON name. A pass snapshots the settings before
/// discovery and compares after <see cref="CatalogueIndexingSource.CaptureFrom"/>, so only members the
/// pipeline actually moved (expensive-query flags, show counts, playlist history) are written back. A member
/// a curator edited mid-pass (e.g. the YouTube channel id) is therefore left as the curator saved it.
/// </summary>
public static class CatalogueIndexingChanges
{
    public static JsonObject Snapshot(CatalogueIndexingSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return JsonSerializer.SerializeToNode(source) as JsonObject
               ?? throw new InvalidOperationException("Indexing settings did not serialise to a JSON object.");
    }

    public static IReadOnlyCollection<string> ChangedSince(JsonObject before, CatalogueIndexingSource after)
    {
        ArgumentNullException.ThrowIfNull(before);
        var current = Snapshot(after);
        return current
            .Select(pair => pair.Key)
            .Union(before.Select(pair => pair.Key))
            .Where(name => !JsonNode.DeepEquals(before[name], current[name]))
            .Order(StringComparer.Ordinal)
            .ToList();
    }
}
