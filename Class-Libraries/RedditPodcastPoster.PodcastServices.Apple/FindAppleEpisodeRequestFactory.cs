﻿using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple;

public static class FindAppleEpisodeRequestFactory
{
    public static FindAppleEpisodeRequest Create(Podcast podcast, Episode episode)
    {
        return new FindAppleEpisodeRequest(
            podcast.AppleId,
            podcast.Name,
            episode.AppleId,
            episode.Title,
            episode.Release,
            podcast.ReleaseAuthority,
            episode.Length,
            podcast.Episodes.ToList().FindIndex(x => x == episode)
        );
    }

    public static FindAppleEpisodeRequest Create(iTunesSearch.Library.Models.Podcast podcast,
        PodcastServiceSearchCriteria criteria)
    {
        return new FindAppleEpisodeRequest(
            podcast.Id,
            podcast.Name,
            null,
            criteria.EpisodeTitle,
            criteria.Release,
            null,
            criteria.Duration,
            0);
    }

    public static FindAppleEpisodeRequest Create(long podcastId, long episodeId)
    {
        return new FindAppleEpisodeRequest(
            podcastId,
            string.Empty,
            episodeId,
            string.Empty,
            null,
            null,
            null,
            0);
    }
}