using System.Text.Json.Serialization;

namespace RedditPodcastPoster.PodcastServices.Taddy.Dtos;

public class SearchForTermResponse
{
    [JsonPropertyName("searchId")]
    public string SearchId { get; set; }

    [JsonPropertyName("podcastEpisodes")]
    public PodcastEpisode[] Episodes { get; set; }
}