using System.Text.Json.Serialization;

namespace RedditPodcastPoster.PodcastServices.Taddy.Dtos;

public class SearchResponse
{
    [JsonPropertyName("searchForTerm")]
    public required SearchForTermResponse Results { get; set; }
}