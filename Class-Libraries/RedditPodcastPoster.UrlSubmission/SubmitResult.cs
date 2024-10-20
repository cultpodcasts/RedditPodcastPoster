﻿using static RedditPodcastPoster.UrlSubmission.SubmitResult;

namespace RedditPodcastPoster.UrlSubmission;

public record SubmitResult(
    SubmitResultState EpisodeResult,
    SubmitResultState PodcastResult,
    Guid? EpisodeId = null)
{
    public enum SubmitResultState
    {
        None = 0,
        Created,
        Enriched,
        PodcastRemoved,
        EpisodeAlreadyExists
    }
}