using System.Text.Json.Serialization;

namespace RedditPodcastPoster.CloudflareRedirect.Dtos;

public class GetListItemsResultInfo
{
    [JsonPropertyName("cursors")]
    public GetListItemsResultInfoCursors Cursors { get; set; }
}