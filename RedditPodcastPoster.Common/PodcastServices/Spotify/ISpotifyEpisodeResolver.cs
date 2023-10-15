﻿namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public interface ISpotifyEpisodeResolver
{
    Task<FindEpisodeResponse> FindEpisode(FindSpotifyEpisodeRequest request, IndexingContext indexingContext);
    Task<PaginateEpisodesResponse> GetEpisodes(SpotifyPodcastId request, IndexingContext indexingContext);
}