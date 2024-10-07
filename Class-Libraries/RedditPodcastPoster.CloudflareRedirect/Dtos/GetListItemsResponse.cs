using System.Text.Json.Serialization;

namespace RedditPodcastPoster.CloudflareRedirect.Dtos;

public class GetListItemsResponse
{
    [JsonPropertyName("errors")]
    public dynamic Errors { get; set; }

    [JsonPropertyName("messages")]
    public dynamic Messages { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("result")]
    public GetListItemsResult[] Results { get; set; }

    [JsonPropertyName("result_info")]
    public GetListItemsResultInfo? Info { get; set; }
}