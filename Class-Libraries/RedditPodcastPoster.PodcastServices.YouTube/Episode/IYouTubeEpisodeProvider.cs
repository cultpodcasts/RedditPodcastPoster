using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.Models;
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

    Task<GetPlaylistEpisodesResponse> GetPlaylistEpisodes(
        YouTubePlaylistId youTubePlaylistId,
        YouTubeChannelId? youTubeChannelId,
        IndexingContext indexingContext, 
        bool expensivePlaylist = false);
}
