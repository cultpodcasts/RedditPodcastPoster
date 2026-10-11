using System.Text.Json;

namespace Api.Handlers;

/// <summary>
/// Normalises claim values: the auth front-end may flatten a JSON array claim into repeated claims,
/// or pass it through as one JSON-array string. Both yield the same distinct, non-blank values.
/// </summary>
public static class ClaimValueParser
{
    public static IReadOnlyList<string> Parse(IEnumerable<string?> rawValues)
    {
        var values = new List<string>();
        foreach (var raw in rawValues)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var trimmed = raw.Trim();
            if (trimmed.StartsWith('['))
            {
                try
                {
                    values.AddRange(JsonSerializer.Deserialize<string?[]>(trimmed)?
                        .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()) ?? []);
                    continue;
                }
                catch (JsonException)
                {
                    // Not a JSON array; treat as a literal value.
                }
            }

            values.Add(trimmed);
        }

        return values.Distinct(StringComparer.Ordinal).ToArray();
    }
}
