namespace RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;

public readonly record struct ThumbnailCandidate(Uri Url, long Height, bool IsDefaultTier);
