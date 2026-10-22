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
        YouTubeChannelId? youTubeChannelId,
        IndexingContext indexingContext, 
        bool expensivePlaylist = false,
        PlaylistOrder? playlistOrder = null,
        IEnumerable<string>? knownIds = null);

    /// <summary>
    /// The playlist path's build step on items already fetched: date window, known-id skip, videos.list, then the
    /// deleted / other-channel / members-only filters and <c>BuildEpisodeAsync</c>. The full walk and the YouTube
    /// indexing hints uploads check both use it, so both filter identically. Results is null when videos.list or a
    /// build failed.
    /// </summary>
    Task<GetPlaylistEpisodesResponse> BuildPlaylistEpisodes(
        Podcast podcast,
        YouTubeChannelId? youTubeChannelId,
        IndexingContext indexingContext,
        IList<PlaylistItem> playlistItems,
        IEnumerable<string>? knownIds = null);
}
