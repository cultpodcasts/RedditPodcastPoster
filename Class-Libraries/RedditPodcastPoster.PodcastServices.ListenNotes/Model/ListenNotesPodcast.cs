using Newtonsoft.Json;

namespace RedditPodcastPoster.PodcastServices.ListenNotes.Model;

public class ListenNotesPodcast
{
    [JsonProperty("title_original")]
    public required string ShowName { get; set; }

    [JsonProperty("publisher_original")]
    public required string Publisher { get; set; }
}