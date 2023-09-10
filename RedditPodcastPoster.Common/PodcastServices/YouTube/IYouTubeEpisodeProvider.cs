﻿using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public interface IYouTubeEpisodeProvider
{
    Task<IList<Episode>> GetEpisodes(YouTubeGetEpisodesRequest request);
    Episode GetEpisode(SearchResult searchResult, Video videoDetails);
    Episode GetEpisode(PlaylistItemSnippet playlistItemSnippet, Video videoDetails);
}