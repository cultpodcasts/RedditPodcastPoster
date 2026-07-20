using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Episode;

public interface IYouTubeEpisodeProvider
{
    Task<IList<RedditPodcastPoster.Models.Episode>?> GetEpisodes(
        RedditPodcastPoster.Models.Podcast podcast,
        IndexingContext indexingContext,
        IEnumerable<string> knownIds);

    Task<RedditPodcastPoster.Models.Episode> GetEpisodeAsync(
        SearchResult searchResult,
        Google.Apis.YouTube.v3.Data.Video videoDetails);

    Task<RedditPodcastPoster.Models.Episode> GetEpisodeAsync(
        PlaylistItemSnippet playlistItemSnippet,
        Google.Apis.YouTube.v3.Data.Video videoDetails);

    Task<GetPlaylistEpisodesResponse> GetPlaylistEpisodes(
        YouTubePlaylistId youTubePlaylistId,
        YouTubeChannelId? youTubeChannelId,
        IndexingContext indexingContext, 
        bool expensivePlaylist = false);
}
