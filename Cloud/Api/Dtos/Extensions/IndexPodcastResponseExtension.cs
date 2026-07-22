using RedditPodcastPoster.Indexing.Models;

namespace Api.Dtos.Extensions;

public static class IndexPodcastResponseExtension
{
    public static IndexPodcastResponse ToDto(this IndexResponse indexResponse, SearchIndexerState indexed)
    {
        if (indexResponse.UpdatedEpisodes == null)
        {
            return new IndexPodcastResponse { IndexStatus = indexResponse.IndexStatus };
        }

        return new IndexPodcastResponse
        {
            IndexStatus = indexResponse.IndexStatus,
            IndexedEpisodes = indexResponse.UpdatedEpisodes.Select(x => x.ToDto()).ToArray(),
            SearchIndexerState = indexed
        };
    }

    private static IndexPodcastResponse.IndexedEpisode ToDto(
        this RedditPodcastPoster.Indexing.Models.IndexedEpisode indexedEpisode)
    {
        return new IndexPodcastResponse.IndexedEpisode
        {
            EpisodeId = indexedEpisode.Episode.Id,
            PodcastId = indexedEpisode.Episode.PodcastId,
            Spotify = indexedEpisode.Spotify,
            Apple = indexedEpisode.Apple,
            YouTube = indexedEpisode.YouTube,
            Subjects = indexedEpisode.Subjects
        };
    }
}
