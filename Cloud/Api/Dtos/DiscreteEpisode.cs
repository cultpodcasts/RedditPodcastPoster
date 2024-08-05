using System.Text.Json.Serialization;

namespace Api.Dtos;

public class DiscreteEpisode : RedditPodcastPoster.Models.Episode
{
    [JsonPropertyName("podcastName")]
    [JsonPropertyOrder(30)]
    public string PodcastName { get; set; } = "";

}