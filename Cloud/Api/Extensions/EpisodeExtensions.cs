﻿using Api.Dtos;
using RedditPodcastPoster.Models;

namespace Api.Extensions;

public static class EpisodeExtensions
{
    public static DiscreteEpisode Enrich(this Episode episode, string podcastName)
    {
        return new DiscreteEpisode
        {
            Id = episode.Id,
            PodcastName = podcastName,
            Title = episode.Title,
            Description = episode.Description,
            Posted = episode.Posted,
            Tweeted = episode.Tweeted,
            BlueskyPosted = episode.BlueskyPosted,
            Ignored = episode.Ignored,
            Release = episode.Release,
            Removed = episode.Removed,
            Length = episode.Length,
            Explicit = episode.Explicit,
            SpotifyId = episode.SpotifyId,
            AppleId = episode.AppleId,
            YouTubeId = episode.YouTubeId,
            Urls = episode.Urls,
            Subjects = episode.Subjects,
            SearchTerms = episode.SearchTerms
        };
    }
}