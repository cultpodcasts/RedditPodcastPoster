﻿using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Strategies;

public interface IYouTubeApiKeyStrategy
{
    ApplicationWrapper GetApplication(ApplicationUsage usage);
    ApplicationWrapper GetApplication(ApplicationUsage usage, int index, int reattempt);
}