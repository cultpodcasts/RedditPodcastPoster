namespace RedditPodcastPoster.PodcastServices.YouTube.Models;

/// <summary>Why a channels.list lookup did or did not produce a channel.</summary>
public enum YouTubeChannelLookupOutcome
{
    /// <summary>Unset. Never a successful lookup, so a defaulted outcome cannot read as success.</summary>
    Unknown = 0,

    /// <summary>The API returned the channel (or it came from the pass cache).</summary>
    Found = 1,

    /// <summary>No API call was made because SkipYouTubeUrlResolving is set for this pass and nothing was cached.</summary>
    Skipped,

    /// <summary>The API answered successfully with no items: the channel id does not exist or is not public.</summary>
    NotFound,

    /// <summary>A non-quota Google API or unexpected error occurred.</summary>
    ApiError,

    /// <summary>Every api-key in the rotation ring hit its quota.</summary>
    QuotaExhausted
}

/// <summary>
/// Result of a channels.list lookup. <see cref="Channel"/> is set if, and only if, <see cref="Outcome"/> is
/// <see cref="YouTubeChannelLookupOutcome.Found"/>; use <see cref="Found"/> and <see cref="Of"/> to create one.
/// </summary>
public sealed record YouTubeChannelLookupResult
{
    private YouTubeChannelLookupResult(Google.Apis.YouTube.v3.Data.Channel? channel, YouTubeChannelLookupOutcome outcome)
    {
        Channel = channel;
        Outcome = outcome;
    }

    public Google.Apis.YouTube.v3.Data.Channel? Channel { get; }

    public YouTubeChannelLookupOutcome Outcome { get; }

    public static YouTubeChannelLookupResult Found(Google.Apis.YouTube.v3.Data.Channel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        return new YouTubeChannelLookupResult(channel, YouTubeChannelLookupOutcome.Found);
    }

    public static YouTubeChannelLookupResult Of(YouTubeChannelLookupOutcome outcome)
    {
        if (outcome is YouTubeChannelLookupOutcome.Found or YouTubeChannelLookupOutcome.Unknown)
        {
            throw new ArgumentException(
                $"Outcome '{outcome}' cannot be created without a channel; use {nameof(Found)} for a found channel.",
                nameof(outcome));
        }

        return new YouTubeChannelLookupResult(null, outcome);
    }
}
