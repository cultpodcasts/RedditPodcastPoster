using System.Globalization;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Extensions;

public static class SpotifyEpisodeExtensions
{
    private const string SpotifyDateFormat = "yyyy-MM-dd";

    /// <summary>
    /// Logged when Spotify omits <c>restrictions</c> or does not include a <c>reason</c> value.
    /// </summary>
    public const string AbsentRestrictionReason = "(none)";

    /// <summary>
    /// Spotify marks paywalled episodes with <c>is_playable=false</c> when a market is requested.
    /// Use <see cref="GetSpotifyRestrictionReason(SimpleEpisode)"/> for the accompanying reason when logging skips.
    /// </summary>
    public static bool IsSpotifyFree(this SimpleEpisode episode) => episode.IsPlayable;

    /// <inheritdoc cref="IsSpotifyFree(SimpleEpisode)"/>
    public static bool IsSpotifyFree(this FullEpisode episode) => episode.IsPlayable;

    /// <summary>
    /// Returns Spotify <c>restrictions.reason</c> when captured on our episode subclass; otherwise <see cref="AbsentRestrictionReason"/>.
    /// </summary>
    public static string GetSpotifyRestrictionReason(this SimpleEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        return GetReasonFromMap(episode is SimpleEpisodeWithRestrictions withRestrictions
            ? withRestrictions.Restrictions
            : null);
    }

    /// <inheritdoc cref="GetSpotifyRestrictionReason(SimpleEpisode)"/>
    public static string GetSpotifyRestrictionReason(this FullEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        return GetReasonFromMap(episode is FullEpisodeWithRestrictions withRestrictions
            ? withRestrictions.Restrictions
            : null);
    }

    private static string GetReasonFromMap(IReadOnlyDictionary<string, string>? restrictions)
    {
        if (restrictions != null &&
            restrictions.TryGetValue("reason", out var reason) &&
            !string.IsNullOrWhiteSpace(reason))
        {
            return reason;
        }

        return AbsentRestrictionReason;
    }

    public static DateTime GetReleaseDate(this SimpleEpisode episode)
    {
        if (episode.ReleaseDate is null or "0000")
        {
            return DateTime.MinValue;
        }

        return DateTime.ParseExact(episode.ReleaseDate, SpotifyDateFormat, CultureInfo.InvariantCulture);
    }

    public static DateTime GetReleaseDate(this FullEpisode episode)
    {
        if (episode.ReleaseDate is null or "0000")
        {
            return DateTime.MinValue;
        }

        return DateTime.ParseExact(episode.ReleaseDate, SpotifyDateFormat, CultureInfo.InvariantCulture);
    }

    public static TimeSpan GetDuration(this SimpleEpisode episode)
    {
        return TimeSpan.FromMilliseconds(episode.DurationMs);
    }

    public static TimeSpan GetDuration(this FullEpisode episode)
    {
        return TimeSpan.FromMilliseconds(episode.DurationMs);
    }
}
