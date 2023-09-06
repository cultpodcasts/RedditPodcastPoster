﻿using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public static class PodcastExtensions
{
    public static FindApplePodcastRequest ToFindApplePodcastRequest(this Podcast podcast)
    {
        return new FindApplePodcastRequest(podcast.AppleId, podcast.Name, podcast.Publisher);
    }
}