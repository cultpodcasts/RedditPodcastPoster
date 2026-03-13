using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Episode;

public interface IYouTubeEpisodeProvider
{
    Task<IList<RedditPodcastPoster.Models.V2.Episode>?> GetEpisodes(
        YouTubeChannelId request,
        IndexingContext indexingContext,
        IEnumerable<string> knownIds);

    RedditPodcastPoster.Models.V2.Episode GetEpisode(
        SearchResult searchResult,
        Google.Apis.YouTube.v3.Data.Video videoDetails);

    RedditPodcastPoster.Models.V2.Episode GetEpisode(
        PlaylistItemSnippet playlistItemSnippet,
        Google.Apis.YouTube.v3.Data.Video videoDetails);

    Task<GetPlaylistEpisodesResponse> GetPlaylistEpisodes(
        YouTubePlaylistId youTubePlaylistId,
        YouTubeChannelId? youTubeChannelId,
        IndexingContext indexingContext);
}