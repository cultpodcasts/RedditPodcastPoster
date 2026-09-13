namespace RedditPodcastPoster.Models.Podcasts;

/// <summary>
/// Cosmos SQL fragments for the Azure AI Search pull-path datasource.
/// Callers pass the composed catalog's search-encoded keys and image-coalesce order
/// so new streaming keys cannot be omitted from <c>svc</c> / <c>image</c> while C#
/// push-path indexing includes them. Models does not own streaming URL grammar.
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
    /// <summary>
    /// <c>RTRIM(CONCAT(...), "|")</c> projecting every non-index-id catalog URL into <c>svc</c>
    /// as <c>key:</c> + raw URL (pull dialect; see type remarks).
    /// </summary>
    public static string SvcProjection(IReadOnlyList<string> searchEncodedKeys)
    {
        ArgumentNullException.ThrowIfNull(searchEncodedKeys);
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
        ArgumentNullException.ThrowIfNull(imageCoalesceOrder);
        return string.Join(
            " ?? ",
            imageCoalesceOrder.Select(key => $"e.services.{key}.image"));
    }
}
