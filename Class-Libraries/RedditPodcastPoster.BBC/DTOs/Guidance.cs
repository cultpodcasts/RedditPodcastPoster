using System.Text.Json.Serialization;

namespace RedditPodcastPoster.BBC.DTOs
{
    public class Guidance
    {
        [JsonPropertyName("warnings")]
        public Dictionary<string, string>? Warnings { get; set; }

        public bool HasWarnings => Warnings != null && Warnings.Any();
    }
}