﻿using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public interface IYouTubeEpisodeProvider
{
    Task<IList<Episode>?> GetEpisodes(YouTubeChannelId request, IndexOptions indexOptions);
    Episode GetEpisode(SearchResult searchResult, Video videoDetails);
    Episode GetEpisode(PlaylistItemSnippet playlistItemSnippet, Video videoDetails);

    Task<IList<Episode>?> GetPlaylistEpisodes(YouTubePlaylistId youTubePlaylistId,
        IndexOptions indexOptions);
}