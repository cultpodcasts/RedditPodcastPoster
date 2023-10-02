﻿using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.UrlCategorisation;

public interface IYouTubeUrlCategoriser : IPodcastServiceUrlResolver
{
    Task<ResolvedYouTubeItem?> Resolve(IList<Podcast> podcasts, Uri url, IndexingContext indexingContext);

    Task<ResolvedYouTubeItem?> Resolve(PodcastServiceSearchCriteria criteria, Podcast? matchingPodcast,
        IndexingContext indexingContext);
}