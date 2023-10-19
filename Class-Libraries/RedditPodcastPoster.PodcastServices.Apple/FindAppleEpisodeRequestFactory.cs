﻿using RedditPodcastPoster.Models;

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
            podcast.Episodes.ToList().FindIndex(x => x == episode)
        );
    }
}