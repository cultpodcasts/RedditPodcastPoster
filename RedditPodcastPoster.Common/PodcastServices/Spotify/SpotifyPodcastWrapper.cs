﻿using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public class SpotifyPodcastWrapper
{
    public SpotifyPodcastWrapper(FullShow? fullShow = null, SimpleShow? simpleShow = null,
        bool expensiveQueryFound = false)
    {
        FullShow = fullShow;
        SimpleShow = simpleShow;
    }

    public string Id => FullShow?.Id ?? SimpleShow?.Id ?? string.Empty;

    public FullShow? FullShow { get; init; }
    public SimpleShow? SimpleShow { get; init; }
    public bool ExpensiveQueryFound { get; init; }
}