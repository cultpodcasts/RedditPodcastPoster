﻿namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public record GetEpisodesRequest(SpotifyPodcastId SpotifyPodcastId, bool HasExpensiveSpotifyEpisodesQuery=false);