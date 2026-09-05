using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.EntitySearchIndexer.Models;

/// <summary>
/// Cosmos SQL fragments for the Azure AI Search pull-path datasource.
/// Kept in lock-step with <see cref="ServiceCatalog.SearchEncodedKeys"/> and
/// <see cref="ServiceCatalog.ImageCoalesceOrder"/> so new streaming keys (ITVX, Channel 4, …)
/// cannot be omitted from <c>svc</c> / <c>image</c> while C# push-path indexing includes them.
/// </summary>
public static class SearchIndexCosmosSql
{
    /// <summary>
    /// <c>RTRIM(CONCAT(...), "|")</c> projecting every non-index-id catalog URL into <c>svc</c>.
    /// </summary>
    public static string SvcProjection()
    {
        var parts = ServiceCatalog.SearchEncodedKeys.Select(key =>
            $@"IIF(IS_DEFINED(e.services.{key}.url), CONCAT(""{key}:"", e.services.{key}.url, ""|""), """")");
        return $@"RTRIM(CONCAT({string.Join(", ", parts)}), ""|"")";
    }

    /// <summary>
    /// Null-coalescing chain of <c>e.services.*.image</c> in
    /// <see cref="ServiceCatalog.ImageCoalesceOrder"/> (YouTube-first).
    /// </summary>
    public static string CoalescedImageFallback() =>
        string.Join(
            " ?? ",
            ServiceCatalog.ImageCoalesceOrder.Select(key => $"e.services.{key}.image"));
}
