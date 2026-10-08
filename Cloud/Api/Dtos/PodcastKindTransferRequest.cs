using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Catalogue;

namespace Api.Dtos;

public class PodcastKindTransferRequest
{
    [JsonPropertyName("targetKind")]
    public CatalogueParentKind? TargetKind { get; set; }
}
