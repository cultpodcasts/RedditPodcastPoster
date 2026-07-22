using System.Text.Json.Serialization;
using RedditPodcastPoster.Indexing.Models;

namespace Api.Dtos;

public class IndexPodcastResponse
{
    [JsonPropertyName("indexedEpisodes")]
    public IndexedEpisode[]? IndexedEpisodes { get; set; }

    [JsonPropertyName("indexStatus")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required IndexStatus IndexStatus { get; set; }

    [JsonPropertyName("searchIndexerState")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SearchIndexerState SearchIndexerState { get; set; }

    public class IndexedEpisode
    {
        [JsonPropertyName("podcastId")]
        public required Guid PodcastId { get; set; }

        [JsonPropertyName("episodeId")]
        public required Guid EpisodeId { get; set; }

        [JsonPropertyName("spotify")]
        public required bool Spotify { get; set; }

        [JsonPropertyName("apple")]
        public required bool Apple { get; set; }

        [JsonPropertyName("youtube")]
        public required bool YouTube { get; set; }

        [JsonPropertyName("subjects")]
        public required string[] Subjects { get; set; }
    }
}
