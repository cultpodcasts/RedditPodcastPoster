using Microsoft.Azure.Cosmos;

namespace EpisodeWireRewrite;

/// <summary>Builds Cosmos patch ops to move a document to a desired cutover-field state.</summary>
public static class WireCutoverPatchBuilder
{
    public static IReadOnlyList<PatchOperation> Build(IReadOnlyList<WireFieldState> desired)
    {
        ArgumentNullException.ThrowIfNull(desired);
        var ops = new List<PatchOperation>(desired.Count);
        foreach (var field in desired)
        {
            var path = "/" + field.Name;
            if (!field.Exists || field.JsonValue is null)
            {
                ops.Add(PatchOperation.Remove(path));
            }
            else
            {
                ops.Add(PatchOperation.Set(path, ParseToken(field.JsonValue)));
            }
        }

        return ops;
    }

    /// <summary>
    /// Prefer Set+Remove that match desired vs current so Remove on missing props does not 400.
    /// When current is unknown, use <see cref="Build"/> and tolerate Cosmos remove-not-found if needed.
    /// </summary>
    public static IReadOnlyList<PatchOperation> BuildDelta(
        IReadOnlyList<WireFieldState> current,
        IReadOnlyList<WireFieldState> desired)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(desired);
        if (current.Count != desired.Count)
        {
            throw new ArgumentException("current and desired must list the same cutover paths.");
        }

        var ops = new List<PatchOperation>();
        for (var i = 0; i < desired.Count; i++)
        {
            var from = current[i];
            var to = desired[i];
            if (!string.Equals(from.Name, to.Name, StringComparison.Ordinal))
            {
                throw new ArgumentException($"Path mismatch at {i}: {from.Name} vs {to.Name}.");
            }

            if (from.Exists == to.Exists &&
                string.Equals(from.JsonValue, to.JsonValue, StringComparison.Ordinal))
            {
                continue;
            }

            var path = "/" + to.Name;
            if (!to.Exists || to.JsonValue is null)
            {
                if (from.Exists)
                {
                    ops.Add(PatchOperation.Remove(path));
                }
            }
            else
            {
                ops.Add(PatchOperation.Set(path, ParseToken(to.JsonValue)));
            }
        }

        return ops;
    }

    private static object? ParseToken(string jsonValue)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(jsonValue);
        return doc.RootElement.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => doc.RootElement.GetString(),
            System.Text.Json.JsonValueKind.Number when doc.RootElement.TryGetInt64(out var l) => l,
            System.Text.Json.JsonValueKind.Number when doc.RootElement.TryGetDouble(out var d) => d,
            System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonValueKind.False => false,
            System.Text.Json.JsonValueKind.Null => null,
            _ => System.Text.Json.JsonSerializer.Deserialize<object>(jsonValue)
        };
    }
}
