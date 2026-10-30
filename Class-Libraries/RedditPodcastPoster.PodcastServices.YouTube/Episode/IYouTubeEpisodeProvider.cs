using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using EpisodeModel = RedditPodcastPoster.Models.Episodes.Episode;

namespace RedditPodcastPoster.PodcastServices.YouTube.Episode;

public interface IYouTubeEpisodeProvider
{
    Task<IList<EpisodeModel>?> GetEpisodes(
        Podcast podcast,
        IndexingContext indexingContext,
        IEnumerable<string> knownIds);

    Task<EpisodeModel> GetEpisodeAsync(
        SearchResult searchResult,
        Google.Apis.YouTube.v3.Data.Video videoDetails);

    Task<EpisodeModel> GetEpisodeAsync(
        PlaylistItemSnippet playlistItemSnippet,
        Google.Apis.YouTube.v3.Data.Video videoDetails);

    /// <param name="expensivePlaylist">
    /// Ignored: every playlist walk pages at <see cref="Playlist.YouTubePlaylistService.PlaylistPageSize"/>.
    /// Kept for call-site compatibility; removal is tracked in #1104.
    /// </param>
    Task<GetPlaylistEpisodesResponse> GetPlaylistEpisodes(
        Podcast podcast,
        YouTubePlaylistId youTubePlaylistId,
        IndexingContext indexingContext,
        bool expensivePlaylist = false,
        PlaylistOrder? playlistOrder = null,
        IEnumerable<string>? knownIds = null);

    /// <summary>
    /// The playlist path's build step on items already fetched: date window, known-id skip, videos.list, then the
    /// deleted / members-only filters and <c>BuildEpisodeAsync</c>. Every video in the playlist is kept whichever
    /// channel uploaded it: when a parent has a playlist, the playlist alone is its YouTube source. The full walk and the YouTube
    /// indexing hints uploads check both use it, so both filter identically. Results is null when videos.list or a
    /// build failed.
    /// </summary>
    Task<GetPlaylistEpisodesResponse> BuildPlaylistEpisodes(
        Podcast podcast,
        IndexingContext indexingContext,
        IList<PlaylistItem> playlistItems,
        IEnumerable<string>? knownIds = null);
}
