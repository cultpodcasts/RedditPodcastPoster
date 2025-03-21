﻿using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Services;

public interface IYouTubeUrlCategoriser
{
    Task<ResolvedYouTubeItem?> Resolve(Podcast? podcasts, Uri url, IndexingContext indexingContext);

    Task<ResolvedYouTubeItem?> Resolve(PodcastServiceSearchCriteria criteria, Podcast? matchingPodcast,
        IndexingContext indexingContext);
}