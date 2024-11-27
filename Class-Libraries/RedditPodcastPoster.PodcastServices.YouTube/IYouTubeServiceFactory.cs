﻿using RedditPodcastPoster.PodcastServices.YouTube.Configuration;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public interface IYouTubeServiceFactory
{
    YouTubeServiceWrapper Create(ApplicationUsage usage);
    YouTubeServiceWrapper Create(ApplicationUsage usage, int index, int reattempt);
}