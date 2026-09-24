using System.Globalization;
using System.Text.Json;
using RedditPodcastPoster.Models.Catalogue;

namespace EpisodeWireRewrite;

/// <summary>
/// Pure Episode JSON cutover planner. Operates on raw Cosmos JSON so leftover keys
/// are visible. Rollback restores the <see cref="WireCutoverChange.Before"/> snapshot.
/// </summary>
public static class WireCutoverPlanner
{
    private static readonly JsonSerializerOptions ReleaseJson = new();

    public static bool NeedsCutover(JsonElement root) =>
        TryCreateChange(root, out _);

    public static bool TryCreateChange(JsonElement root, out WireCutoverChange? change)
    {
        change = null;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!TryReadRef(root, out var episodeRef))
        {
            return false;
        }

        if (root.TryGetProperty(WireCutoverFields.Type, out var typeEl))
        {
            var isEpisode = typeEl.ValueKind switch
            {
                JsonValueKind.Number => typeEl.TryGetInt32(out var n) && n == 2,
                JsonValueKind.String => string.Equals(typeEl.GetString(), "episode", StringComparison.OrdinalIgnoreCase)
                                       || string.Equals(typeEl.GetString(), "Episode", StringComparison.Ordinal),
                _ => true
            };
            if (!isEpisode)
            {
                return false;
            }
        }

        var before = Capture(root);
        var after = ComputeAfter(root, before);
        if (StatesEqual(before, after))
        {
            return false;
        }

        change = new WireCutoverChange
        {
            EpisodeId = episodeRef.EpisodeId,
            PodcastId = episodeRef.PodcastId,
            Before = before,
            After = after,
            FieldChanges = Diff(before, after),
            Reason = Describe(before, after)
        };
        return true;
    }

    public static IReadOnlyList<WireFieldChange> Diff(
        IReadOnlyList<WireFieldState> before,
        IReadOnlyList<WireFieldState> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (before.Count != after.Count)
        {
            throw new ArgumentException("before and after must list the same cutover paths.");
        }

        var changes = new List<WireFieldChange>();
        for (var i = 0; i < before.Count; i++)
        {
            if (!string.Equals(before[i].Name, after[i].Name, StringComparison.Ordinal))
            {
                throw new ArgumentException($"Path mismatch at {i}: {before[i].Name} vs {after[i].Name}.");
            }

            if (before[i].Exists == after[i].Exists &&
                string.Equals(before[i].JsonValue, after[i].JsonValue, StringComparison.Ordinal))
            {
                continue;
            }

            changes.Add(new WireFieldChange
            {
                Name = before[i].Name,
                FromExists = before[i].Exists,
                FromJson = before[i].JsonValue,
                ToExists = after[i].Exists,
                ToJson = after[i].JsonValue
            });
        }

        return changes;
    }

    public static IReadOnlyList<WireFieldState> Capture(JsonElement root) =>
        WireCutoverFields.SnapshotPaths
            .Select(name => CaptureField(root, name))
            .ToArray();

    public static IReadOnlyList<WireFieldState> ComputeAfter(
        JsonElement root,
        IReadOnlyList<WireFieldState>? before = null)
    {
        before ??= Capture(root);
        var map = before.ToDictionary(x => x.Name, StringComparer.Ordinal);

        string? PickString(string newName, string legacyName)
        {
            if (map.TryGetValue(newName, out var neu) && neu.Exists && !IsNullOrEmptyJson(neu.JsonValue))
            {
                return neu.JsonValue;
            }

            if (map.TryGetValue(legacyName, out var leg) && leg.Exists && !IsNullOrEmptyJson(leg.JsonValue))
            {
                return leg.JsonValue;
            }

            return null;
        }

        string? PickRaw(string newName, string legacyName)
        {
            if (map.TryGetValue(newName, out var neu) && neu.Exists)
            {
                return neu.JsonValue;
            }

            if (map.TryGetValue(legacyName, out var leg) && leg.Exists)
            {
                return leg.JsonValue;
            }

            return null;
        }

        var publisherSearch = PickString(
            WireCutoverFields.PublisherSearchTerms,
            WireCutoverFields.PodcastSearchTerms);
        var publisherLanguage = PickString(
            WireCutoverFields.PublisherLanguage,
            WireCutoverFields.PodcastLanguage);
        var parentMetadata = PickRaw(
            WireCutoverFields.ParentMetadataVersion,
            WireCutoverFields.PodcastMetadataVersion);
        var parentRemoved = PickRaw(
            WireCutoverFields.ParentRemoved,
            WireCutoverFields.PodcastRemoved);

        string? releaseSortJson = null;
        if (map.TryGetValue(WireCutoverFields.ReleaseSort, out var existingSort) &&
            existingSort.Exists &&
            !IsNullOrEmptyJson(existingSort.JsonValue))
        {
            releaseSortJson = existingSort.JsonValue;
        }
        else if (TryDeriveReleaseSortJson(root, out var derived))
        {
            releaseSortJson = derived;
        }

        return
        [
            Absent(WireCutoverFields.PodcastSearchTerms),
            PresentOrAbsent(WireCutoverFields.PublisherSearchTerms, publisherSearch),
            Absent(WireCutoverFields.PodcastLanguage),
            PresentOrAbsent(WireCutoverFields.PublisherLanguage, publisherLanguage),
            Absent(WireCutoverFields.PodcastMetadataVersion),
            PresentOrAbsent(WireCutoverFields.ParentMetadataVersion, parentMetadata),
            Absent(WireCutoverFields.PodcastRemoved),
            PresentOrAbsent(WireCutoverFields.ParentRemoved, parentRemoved),
            PresentOrAbsent(WireCutoverFields.ReleaseSort, releaseSortJson)
        ];
    }

    /// <summary>
    /// Rollback target is always the migrate <see cref="WireCutoverChange.Before"/> snapshot.
    /// </summary>
    public static IReadOnlyList<WireFieldState> RollbackTarget(WireCutoverChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return change.Before;
    }

    /// <summary>
    /// Apply a desired cutover-field state onto a mutable JSON object (in-memory).
    /// Does not touch other properties.
    /// </summary>
    public static void ApplyState(JsonObjectMutator doc, IReadOnlyList<WireFieldState> desired)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(desired);
        foreach (var field in desired)
        {
            if (!field.Exists || field.JsonValue is null)
            {
                doc.Remove(field.Name);
            }
            else
            {
                doc.SetRaw(field.Name, field.JsonValue);
            }
        }
    }

    public static bool TryReadRef(JsonElement root, out WireEpisodeRef episodeRef)
    {
        episodeRef = default;
        if (!TryReadGuid(root, WireCutoverFields.Id, out var episodeId) ||
            !TryReadGuid(root, WireCutoverFields.PodcastId, out var podcastId))
        {
            return false;
        }

        episodeRef = new WireEpisodeRef(podcastId, episodeId);
        return true;
    }

    public static bool StatesEqual(IReadOnlyList<WireFieldState> a, IReadOnlyList<WireFieldState> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (!string.Equals(a[i].Name, b[i].Name, StringComparison.Ordinal) ||
                a[i].Exists != b[i].Exists ||
                !string.Equals(a[i].JsonValue, b[i].JsonValue, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string Describe(IReadOnlyList<WireFieldState> before, IReadOnlyList<WireFieldState> after)
    {
        var parts = new List<string>();
        for (var i = 0; i < before.Count; i++)
        {
            if (before[i].Exists == after[i].Exists &&
                string.Equals(before[i].JsonValue, after[i].JsonValue, StringComparison.Ordinal))
            {
                continue;
            }

            parts.Add(before[i].Name);
        }

        return string.Join(",", parts);
    }

    private static WireFieldState CaptureField(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return Absent(name);
        }

        return new WireFieldState
        {
            Name = name,
            Exists = true,
            JsonValue = el.GetRawText()
        };
    }

    private static WireFieldState Absent(string name) => new()
    {
        Name = name,
        Exists = false,
        JsonValue = null
    };

    private static WireFieldState PresentOrAbsent(string name, string? jsonValue)
    {
        if (jsonValue is null)
        {
            return Absent(name);
        }

        return new WireFieldState
        {
            Name = name,
            Exists = true,
            JsonValue = jsonValue
        };
    }

    private static bool IsNullOrEmptyJson(string? jsonValue)
    {
        if (string.IsNullOrWhiteSpace(jsonValue))
        {
            return true;
        }

        if (jsonValue is "null" or "\"\"")
        {
            return true;
        }

        return false;
    }

    private static bool TryDeriveReleaseSortJson(JsonElement root, out string json)
    {
        json = null!;
        if (!root.TryGetProperty(WireCutoverFields.Release, out var releaseEl) ||
            releaseEl.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return false;
        }

        try
        {
            var release = JsonSerializer.Deserialize<CatalogueRelease>(releaseEl.GetRawText(), ReleaseJson);
            if (release is null)
            {
                return false;
            }

            var sort = release.ToSortUtc();
            json = "\"" + sort.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture) + "\"";
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadGuid(JsonElement root, string name, out Guid id)
    {
        id = default;
        if (!root.TryGetProperty(name, out var el))
        {
            return false;
        }

        if (el.ValueKind == JsonValueKind.String && Guid.TryParse(el.GetString(), out id))
        {
            return true;
        }

        return false;
    }
}

/// <summary>Minimal mutable JSON object for in-memory apply/rollback tests.</summary>
public sealed class JsonObjectMutator
{
    private readonly Dictionary<string, JsonElement> _props;

    public JsonObjectMutator(JsonElement root)
    {
        _props = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var prop in root.EnumerateObject())
        {
            _props[prop.Name] = prop.Value.Clone();
        }
    }

    public static JsonObjectMutator Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return new JsonObjectMutator(doc.RootElement);
    }

    public void Remove(string name) => _props.Remove(name);

    public void SetRaw(string name, string jsonValue)
    {
        using var doc = JsonDocument.Parse(jsonValue);
        _props[name] = doc.RootElement.Clone();
    }

    public string GetRawText()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var (name, value) in _props)
            {
                writer.WritePropertyName(name);
                value.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public JsonElement ToElement()
    {
        using var doc = JsonDocument.Parse(GetRawText());
        return doc.RootElement.Clone();
    }

    public bool TryGet(string name, out JsonElement el) => _props.TryGetValue(name, out el);
}
