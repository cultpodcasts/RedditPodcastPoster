namespace RedditPodcastPoster.PodcastServices.YouTube.Models;

/// <summary>Why a channels.list lookup did or did not produce a channel.</summary>
public enum YouTubeChannelLookupOutcome
{
    /// <summary>The API returned the channel (or it came from the pass cache).</summary>
    Found,

    /// <summary>No API call was made because SkipYouTubeUrlResolving is set for this pass.</summary>
    Skipped,

    /// <summary>The API answered successfully with no items: the channel id does not exist or is not public.</summary>
    NotFound,

    /// <summary>A non-quota Google API or unexpected error occurred.</summary>
    ApiError,

    /// <summary>Every api-key in the rotation ring hit its quota.</summary>
    QuotaExhausted
}

/// <summary>Result of a channels.list lookup; <see cref="Channel"/> is set only when <see cref="Outcome"/> is Found.</summary>
public record YouTubeChannelLookupResult(Google.Apis.YouTube.v3.Data.Channel? Channel, YouTubeChannelLookupOutcome Outcome)
{
    public static YouTubeChannelLookupResult Found(Google.Apis.YouTube.v3.Data.Channel channel) =>
        new(channel, YouTubeChannelLookupOutcome.Found);

    public static YouTubeChannelLookupResult Of(YouTubeChannelLookupOutcome outcome) => new(null, outcome);
}
