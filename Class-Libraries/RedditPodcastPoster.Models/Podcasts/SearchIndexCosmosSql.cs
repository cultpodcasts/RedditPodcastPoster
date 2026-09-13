using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Models.Podcasts;

/// <summary>
/// Cosmos SQL fragments for the Azure AI Search pull-path datasource.
/// Callers pass the composed catalog's search-encoded keys and image-coalesce order
/// so new streaming keys cannot be omitted from <c>svc</c> / <c>image</c> while C#
/// push-path indexing includes them. Models does not own streaming URL grammar.
/// Empty or non-identifier keys throw — an unloaded catalog must not emit broken SQL.
/// </summary>
/// <remarks>
/// <para>
/// <c>SvcProjection</c> emits the legacy full-URL dialect (<c>key:</c> + raw
/// <c>e.services.*.url</c>) — not the push-path <c>SearchEpisodeServices.Encode()</c> compact
/// grammar (<c>u</c> prefix / <c>|</c>% escape). Clients accept both; aligning SQL with
/// <c>Encode()</c> is optional follow-up.
/// </para>
/// </remarks>
public static class SearchIndexCosmosSql
{
    private static readonly Regex CosmosIdentifier = new(
        "^[A-Za-z][A-Za-z0-9]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// <c>RTRIM(CONCAT(...), "|")</c> projecting every non-index-id catalog URL into <c>svc</c>
    /// as <c>key:</c> + raw URL (pull dialect; see type remarks).
    /// </summary>
    public static string SvcProjection(IReadOnlyList<string> searchEncodedKeys)
    {
        EnsureCosmosKeys(searchEncodedKeys, nameof(searchEncodedKeys));
        var parts = searchEncodedKeys.Select(key =>
            $@"IIF(IS_DEFINED(e.services.{key}.url), CONCAT(""{key}:"", e.services.{key}.url, ""|""), """")");
        return $@"RTRIM(CONCAT({string.Join(", ", parts)}), ""|"")";
    }

    /// <summary>
    /// Null-coalescing chain of <c>e.services.*.image</c> in the supplied key order
    /// (YouTube-first when callers pass the composed image-coalesce list).
    /// </summary>
    public static string CoalescedImageFallback(IReadOnlyList<string> imageCoalesceOrder)
    {
        EnsureCosmosKeys(imageCoalesceOrder, nameof(imageCoalesceOrder));
        return string.Join(
            " ?? ",
            imageCoalesceOrder.Select(key => $"e.services.{key}.image"));
    }

    private static void EnsureCosmosKeys(IReadOnlyList<string> keys, string paramName)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Count == 0)
        {
            throw new ArgumentException(
                "Key list must not be empty. Pass a loaded StreamingServiceCatalog list so pull-path SQL is not silently blank.",
                paramName);
        }

        foreach (var key in keys)
        {
            if (string.IsNullOrEmpty(key) || !CosmosIdentifier.IsMatch(key))
            {
                throw new ArgumentException(
                    $"'{key}' is not a Cosmos identifier; interpolated SQL keys must match [A-Za-z][A-Za-z0-9]*.",
                    paramName);
            }
        }
    }
}
