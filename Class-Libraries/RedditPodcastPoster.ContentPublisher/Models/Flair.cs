using System.Text.Json.Serialization;

namespace RedditPodcastPoster.ContentPublisher.Models;

public class Flair
{
    [JsonPropertyName("text")]
    [JsonPropertyOrder(1)]
    public string Text { get; set; } = "";

    [JsonPropertyName("textEditable")]
    [JsonPropertyOrder(3)]
    public bool TextEditable { get; set; }

    [JsonPropertyName("textColour")]
    [JsonPropertyOrder(4)]
    public string TextColour { get; set; } = "";

    [JsonPropertyName("backgroundColour")]
    [JsonPropertyOrder(5)]
    public string BackgroundColour { get; set; } = "";
}