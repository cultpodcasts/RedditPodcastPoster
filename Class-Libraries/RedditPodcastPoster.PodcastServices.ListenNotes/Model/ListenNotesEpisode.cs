using Newtonsoft.Json;

namespace RedditPodcastPoster.PodcastServices.ListenNotes.Model;

public class ListenNotesEpisode
{
    [JsonProperty("description_original")]
    public required string Description { get; set; }

    [JsonProperty("title_original")]
    public required string Title { get; set; }

    [JsonProperty("id")]
    public required string Id { get; set; }

    [JsonProperty("pub_date_ms")]
    public long ReleasedMilliseconds { get; set; }

    [JsonProperty("podcast")]
    public required ListenNotesPodcast Podcast { get; set; }
}