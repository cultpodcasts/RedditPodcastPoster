using System.Text.Json.Serialization;
using RedditPodcastPoster.Models;

namespace Api.Dtos;

public class PublicEpisode
{
    [JsonPropertyName("podcastName")]
    [JsonPropertyOrder(30)]
    public string PodcastName { get; set; } = "";
    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public Guid Id { get; set; }
    [JsonPropertyName("title")]
    [JsonPropertyOrder(30)]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    [JsonPropertyOrder(40)]
    public string Description { get; set; } = "";
    [JsonPropertyName("release")]
    [JsonPropertyOrder(70)]
    public DateTime Release { get; set; }

    [JsonPropertyName("duration")]
    [JsonPropertyOrder(71)]
    public TimeSpan Length { get; set; }

    [JsonPropertyName("explicit")]
    [JsonPropertyOrder(72)]
    public bool Explicit { get; set; }
    [JsonPropertyName("urls")]
    [JsonPropertyOrder(100)]
    public ServiceUrls Urls { get; set; } = new();

    [JsonPropertyName("subjects")]
    [JsonPropertyOrder(90)]
    public List<string> Subjects { get; set; } = [];


    [JsonPropertyName("image")]
    [JsonPropertyOrder(250)]
    public Uri? Image { get; set; }
}
