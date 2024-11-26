using System.Globalization;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public static class SpotifyEpisodeExtensions
{
    private const string SpotifyDateFormat = "yyyy-MM-dd";

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
        if (episode.ReleaseDate == "0000")
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