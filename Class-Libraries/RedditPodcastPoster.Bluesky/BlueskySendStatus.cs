﻿namespace RedditPodcastPoster.Bluesky;

public enum BlueskySendStatus
{
    Unknown = 0,
    Success,
    Failure,
    FailureHttp,
    FailureAuth
}