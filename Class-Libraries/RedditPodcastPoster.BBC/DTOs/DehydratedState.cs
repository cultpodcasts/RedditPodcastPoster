using System.Text.Json.Serialization;

namespace RedditPodcastPoster.BBC.DTOs;

public class DehydratedState
{
    [JsonPropertyName("queries")]
    public required Query[] Queries { get; set; }

    public class Query
    {
        [JsonPropertyName("queryKey")]
        public required string[] QueryKey { get; set; }

        [JsonPropertyName("state")]
        public required State State { get; set; }
    }
}