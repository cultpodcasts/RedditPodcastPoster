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

    public static IndexPodcastResponse ToDto(IndexResponse indexResponse, SearchIndexerState indexed)
    {
        if (indexResponse.UpdatedEpisodes == null)
        {
            return new IndexPodcastResponse { IndexStatus = indexResponse.IndexStatus };
        }

        return new IndexPodcastResponse
        {
            IndexStatus = indexResponse.IndexStatus,
            IndexedEpisodes = indexResponse.UpdatedEpisodes.Select(x => ToDto(x)).ToArray(),
            SearchIndexerState = indexed
        };
    }

    private static IndexedEpisode ToDto(
        RedditPodcastPoster.Indexing.Models.IndexedEpisode indexedEpisode,
        bool? indexed = null)
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