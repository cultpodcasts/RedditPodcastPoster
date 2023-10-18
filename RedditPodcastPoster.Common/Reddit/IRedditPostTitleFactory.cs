﻿using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Reddit;

public interface IRedditPostTitleFactory
{
    string ConstructPostTitle(PostModel postModel);
}