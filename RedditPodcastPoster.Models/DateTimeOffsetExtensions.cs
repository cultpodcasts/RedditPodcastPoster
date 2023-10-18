namespace RedditPodcastPoster.Models;

public static class DateTimeOffsetExtensions
{
    public static bool ReleasedSinceDate(this DateTimeOffset? releaseDate, DateTime? date)
    {
        if (releaseDate.HasValue && date.HasValue)
        {
            return releaseDate.Value.ToUniversalTime() >= date.Value.ToUniversalTime();
        }

        return true;
    }
}