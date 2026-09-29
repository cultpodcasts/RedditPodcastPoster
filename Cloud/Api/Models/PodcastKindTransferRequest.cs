using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Catalogue;

namespace Api.Models;

public class PodcastKindTransferRequest
{
    [JsonPropertyName("targetKind")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CatalogueParentKind? TargetKind { get; set; }
}
