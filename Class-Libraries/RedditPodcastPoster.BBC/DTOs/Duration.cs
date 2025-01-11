using System.Text.Json.Serialization;

namespace RedditPodcastPoster.BBC.DTOs
{
    public class Duration
    {
        [JsonPropertyName("value")]
        public int? Seconds { get; set; }

        public TimeSpan? Length => Seconds==null? null: TimeSpan.FromSeconds(Seconds.Value);
    }
}