﻿namespace RedditPodcastPoster.PodcastServices.Abstractions;

public record PodcastServiceSearchCriteria(
    string ShowName,
    string ShowDescription,
    string Publisher,
    string EpisodeTitle,
    string EpisodeDescription,
    DateTime Release,
    TimeSpan Duration)
{
    public string? SpotifyTitle { get; set; }
    public string? AppleTitle { get; set; }
}
