using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public interface IYouTubeEpisodeProvider
{
    Task<IList<Episode>?> GetEpisodes(YouTubeChannelId request, IndexingContext indexingContext,
        IEnumerable<string> knownIds);
    Episode GetEpisode(SearchResult searchResult, Video videoDetails);
    Episode GetEpisode(PlaylistItemSnippet playlistItemSnippet, Video videoDetails);

    Task<GetPlaylistEpisodesResponse> GetPlaylistEpisodes(YouTubePlaylistId youTubePlaylistId,
        IndexingContext indexingContext);
}