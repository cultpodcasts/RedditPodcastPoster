using System.Text.Json.Serialization;

namespace RedditPodcastPoster.BBC.DTOs
{
    public class Synopses
    {
        [JsonPropertyName("short")]
        public string? Short { get; set; }

        [JsonPropertyName("medium")]
        public string? Medium { get; set; }

        [JsonPropertyName("long")]
        public string? Long { get; set; }

        public string Description => Long ?? Medium ?? Short ?? string.Empty;
    }
}