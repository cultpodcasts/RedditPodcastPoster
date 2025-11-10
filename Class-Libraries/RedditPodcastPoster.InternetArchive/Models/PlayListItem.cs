using System.Text.Json.Serialization;
using RedditPodcastPoster.InternetArchive.JsonConverters;

namespace RedditPodcastPoster.InternetArchive.Models
{
    public class PlayListItem
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("orig")]
        public string Orig { get; set; }

        [JsonPropertyName("image"), JsonConverter(typeof(RelativeUriConverter))]
        public Uri Image { get; set; }

        [JsonPropertyName("duration"), JsonConverter(typeof(CustomTimeSpanConverter))]
        public TimeSpan Duration { get; set; }

        [JsonPropertyName("sources")]
        public Source[] Sources { get; set; }

        [JsonPropertyName("tracks")]
        public Track[] Tracks { get; set; }
    }
}

