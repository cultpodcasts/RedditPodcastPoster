using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>Body of POST podcast/{podcastId}/news: the episode ids to move plus the news organisation target.</summary>
public class PodcastNewsMoveRequest : EpisodeNewsMoveRequest
{
    [JsonPropertyName("episodeIds")]
    public Guid[]? EpisodeIds { get; set; }
}
