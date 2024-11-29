using System.Text.Json.Serialization;
using RedditPodcastPoster.Models;

namespace Api.Dtos;

public class DiscreteEpisode : Episode
{
    [JsonPropertyName("podcastName")]
    [JsonPropertyOrder(30)]
    public string PodcastName { get; set; } = "";

    [JsonPropertyName("youTubePodcast")]
    [JsonPropertyOrder(200)]
    public bool YouTubePodcast { get; set; }

    [JsonPropertyName("spotifyPodcast")]
    [JsonPropertyOrder(201)]
    public bool SpotifyPodcast { get; set; }

    [JsonPropertyName("applePodcast")]
    [JsonPropertyOrder(202)]
    public bool ApplePodcast { get; set; }

    [JsonPropertyName("releaseAuthority")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    [JsonPropertyOrder(210)]
    public Service? ReleaseAuthority { get; set; }

    [JsonPropertyName("primaryPostService")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    [JsonPropertyOrder(211)]
    public Service? PrimaryPostService { get; set; }

    [JsonPropertyName("image")]
    [JsonPropertyOrder(250)]
    public Uri? Image { get; set; }
}