using System.Text.Json.Serialization;
using RedditPodcastPoster.Models;

namespace Api.Dtos;

public class DiscoveryResponse
{
    [JsonPropertyName("ids")]
    public required IEnumerable<Guid> Ids { get; set; }

    [JsonPropertyName("results")]
    public required IEnumerable<DiscoveryResult> Results { get; set; }
}