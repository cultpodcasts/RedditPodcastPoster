﻿using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public interface IPodcastEpisodesPoster
{
    Task<IList<ProcessResponse>> PostNewEpisodes(DateTime since, IList<Podcast> podcasts);
}