using System.Globalization;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Extensions;

public static class SpotifyEpisodeExtensions
{
    private const string SpotifyDateFormat = "yyyy-MM-dd";

    /// <summary>
    /// Spotify marks paywalled episodes with <c>is_playable=false</c> when a market is requested.
    /// Prefer this over <c>restrictions.reason</c>, which SpotifyAPI.Web may not deserialize.
    /// </summary>
    public static bool IsSpotifyFree(this SimpleEpisode episode) => episode.IsPlayable;

    /// <inheritdoc cref="IsSpotifyFree(SimpleEpisode)"/>
    public static bool IsSpotifyFree(this FullEpisode episode) => episode.IsPlayable;

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