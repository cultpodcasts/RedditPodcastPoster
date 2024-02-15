using Newtonsoft.Json;

namespace RedditPodcastPoster.PodcastServices.ListenNotes.Model;

public class ListenNotesResponse
{
    [JsonProperty("count")]
    public int Count { get; set; }

    [JsonProperty("next_offset")]
    public int NextOffset { get; set; }

    [JsonProperty("total")]
    public int Total { get; set; }

    [JsonProperty("results")]
    public required IEnumerable<ListenNotesEpisode> Results { get; set; }
}