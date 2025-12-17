namespace RedditPodcastPoster.Models.Extensions;

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

    extension(DateTimeOffset dateTime)
    {
        public DateTimeOffset Floor(TimeSpan interval)
        {
            return dateTime.AddTicks(-(dateTime.Ticks % interval.Ticks));
        }

        public DateTimeOffset Ceiling(TimeSpan interval)
        {
            var overflow = dateTime.Ticks % interval.Ticks;

            return overflow == 0 ? dateTime : dateTime.AddTicks(interval.Ticks - overflow);
        }

        public DateTimeOffset Round(TimeSpan interval)
        {
            var halfIntervalTicks = (interval.Ticks + 1) >> 1;

            return dateTime.AddTicks(halfIntervalTicks - (dateTime.Ticks + halfIntervalTicks) % interval.Ticks);
        }
    }
}