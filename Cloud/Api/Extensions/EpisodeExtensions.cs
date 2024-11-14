﻿using Api.Dtos;
using RedditPodcastPoster.Models;
using Podcast = RedditPodcastPoster.Models.Podcast;

namespace Api.Extensions;

public static class EpisodeExtensions
{
    public static DiscreteEpisode Enrich(this Episode episode, Podcast podcast)
    {
        return new DiscreteEpisode
        {
            Id = episode.Id,
            PodcastName = podcast.Name,
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
            SearchTerms = episode.SearchTerms,
            YouTubePodcast = !string.IsNullOrWhiteSpace(podcast.YouTubeChannelId),
            SpotifyPodcast = !string.IsNullOrWhiteSpace(podcast.SpotifyId),
            ApplePodcast = podcast.AppleId != null,
            ReleaseAuthority = podcast.ReleaseAuthority,
            PrimaryPostService = podcast.PrimaryPostService
        };
    }
}