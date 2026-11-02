namespace RedditPodcastPoster.Twitter.Models;

/// <summary>Stable codes stored on <c>tweetSkipReason</c> when an item cannot form an X post.</summary>
public static class TweetSkipReasons
{
    /// <summary>Handles, hashtags and URLs leave less than the minimum title budget.</summary>
    public const string TitleTooLong = "title-too-long";

    /// <summary>No Spotify, Apple or YouTube URL available to append.</summary>
    public const string NoLink = "no-link";
}
