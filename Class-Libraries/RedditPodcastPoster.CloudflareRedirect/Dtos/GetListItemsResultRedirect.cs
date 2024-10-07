using System.Text.Json.Serialization;

namespace RedditPodcastPoster.CloudflareRedirect.Dtos;

public class GetListItemsResultRedirect
{
    [JsonPropertyName("include_subdomains")]
    public bool IncludeSubdomains { get; set; }

    [JsonPropertyName("preserve_path_suffix")]
    public bool PreservePathSuffix { get; set; }

    [JsonPropertyName("preserve_query_string")]
    public bool PreserveQueryString { get; set; }

    [JsonPropertyName("source_url")]
    public string SourceUrl { get; set; }

    [JsonPropertyName("target_url")]
    public string TargetUrl { get; set; }

    [JsonPropertyName("status_code")]
    public int StatusCode { get; set; }

    [JsonPropertyName("subpath_matching")]
    public bool SubpathMatching { get; set; }
}