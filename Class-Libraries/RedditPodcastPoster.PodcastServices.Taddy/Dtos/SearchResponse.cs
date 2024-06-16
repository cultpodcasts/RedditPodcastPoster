using System.Text.Json.Serialization;

namespace RedditPodcastPoster.PodcastServices.Taddy.Dtos;

public class SearchResponse
{
    [JsonPropertyName("searchForTerm")]
    public SearchForTermResponse Results { get; set; }
}