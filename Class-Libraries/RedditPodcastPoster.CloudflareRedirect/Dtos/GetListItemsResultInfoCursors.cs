using System.Text.Json.Serialization;

namespace RedditPodcastPoster.CloudflareRedirect.Dtos;

public class GetListItemsResultInfoCursors
{
    [JsonPropertyName("before")]
    public string Before { get; set; }

    [JsonPropertyName("after")]
    public string After { get; set; }
}