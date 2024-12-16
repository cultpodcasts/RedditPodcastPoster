﻿namespace RedditPodcastPoster.PodcastServices.Spotify.Extensions;

public static class SpotifyUriExtensions {
    public static Uri CleanSpotifyUrl(this Uri spotifyUrl)
    {
        var spotifyId = SpotifyIdResolver.GetEpisodeId(spotifyUrl);
        return new Uri(spotifyUrl, spotifyId);
    }
}
