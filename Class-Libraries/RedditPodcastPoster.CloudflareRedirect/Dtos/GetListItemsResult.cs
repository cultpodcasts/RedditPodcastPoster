using System.Text.Json.Serialization;

namespace RedditPodcastPoster.CloudflareRedirect.Dtos;

public class GetListItemsResult
{
    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    [JsonPropertyName("hostname")]
    public Hostname? Hostname { get; set; }

    [JsonPropertyName("createdOn")]
    public DateTime? CreatedOn { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("ip")]
    public string? IpAddress { get; set; }

    [JsonPropertyName("modifiedOn")]
    public DateTime? ModifiedOn { get; set; }

    [JsonPropertyName("redirect")]
    public GetListItemsResultRedirect Redirect { get; set; }
}