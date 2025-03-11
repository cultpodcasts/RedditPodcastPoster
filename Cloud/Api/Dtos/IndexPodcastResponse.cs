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

    public static IndexPodcastResponse ToDto(IndexResponse indexResponse)
    {
        if (indexResponse.UpdatedEpisodes == null)
        {
            return new IndexPodcastResponse {IndexStatus = indexResponse.IndexStatus};
        }

        return new IndexPodcastResponse
        {
            IndexStatus = indexResponse.IndexStatus,
            IndexedEpisodes = indexResponse.UpdatedEpisodes.Select(ToDto).ToArray()
        };
    }

    public static IndexedEpisode ToDto(RedditPodcastPoster.Indexing.Models.IndexedEpisode indexedEpisode)
    {
        return new IndexedEpisode
        {
            EpisodeId = indexedEpisode.EpisodeId,
            Spotify = indexedEpisode.Spotify,
            Apple = indexedEpisode.Apple,
            YouTube = indexedEpisode.YouTube,
            Subjects = indexedEpisode.Subjects
        };
    }
}