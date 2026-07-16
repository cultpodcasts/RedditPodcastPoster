using System.Text.Json.Serialization;
using RedditPodcastPoster.InternetArchive.JsonConverters;

namespace RedditPodcastPoster.InternetArchive.Models;

public class Source
{
    [JsonPropertyName("file")]
    public Uri File { get; set; } = null!;

    [JsonPropertyName("type")]
    public string Type { get; set; } = null!;

    [JsonPropertyName("height"), JsonConverter(typeof(CustomIntConverter))]
    public int Height { get; set; }

    [JsonPropertyName("width"), JsonConverter(typeof(CustomIntConverter))]
    public int Width { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = null!;
}