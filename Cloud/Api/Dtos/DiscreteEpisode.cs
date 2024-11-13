using System.Text.Json.Serialization;
using RedditPodcastPoster.Models;

namespace Api.Dtos;

public class DiscreteEpisode : Episode
{
    [JsonPropertyName("podcastName")]
    [JsonPropertyOrder(30)]
    public string PodcastName { get; set; } = "";
}