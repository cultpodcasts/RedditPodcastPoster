﻿namespace RedditPodcastPoster.Indexing.Models;

public record IndexedEpisode(Guid EpisodeId, bool Spotify, bool Apple, bool YouTube)
{
    public string[] Subjects { get; set; } = [];
}