namespace RedditPodcastPoster.PodcastServices.YouTube.CandidatePreference;

/// <summary>Whether a video's auto thumbnails show motion.</summary>
public enum ThumbnailMotion
{
    /// <summary>Thumbnails missing, placeholder, undecodable or timed out. No penalty (fail-open).</summary>
    Unknown = 0,

    /// <summary>The three auto thumbnails are effectively identical: a static-image upload.</summary>
    Static = 1,

    /// <summary>The auto thumbnails differ: real video.</summary>
    Moving = 2
}