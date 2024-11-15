using System.Text.Json.Serialization;
using RedditPodcastPoster.Indexing;

namespace Api.Dtos;

public class IndexPodcastResponse
{
    [JsonPropertyName("indexedEpisodes")]
    public IndexedEpisode[]? IndexedEpisodes { get; set; }

    public static IndexPodcastResponse ToDto(IndexResponse indexResponse)
    {
        if (indexResponse.UpdatedEpisodes == null)
        {
            return new IndexPodcastResponse();
        }

        return new IndexPodcastResponse
        {
            IndexedEpisodes = indexResponse.UpdatedEpisodes.Select(ToDto).ToArray()
        };
    }

    public static IndexedEpisode ToDto(RedditPodcastPoster.Indexing.IndexedEpisode indexedEpisode)
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