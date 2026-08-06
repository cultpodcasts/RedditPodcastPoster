namespace RedditPodcastPoster.Configuration.Extensions;

public static class DateTimeExtensions
{
    private static readonly DateTime UnixEpoch = new(1970, 1, 1);

    public static DateTime DaysAgo(int days)
    {
        {
            return DateOnly
                .FromDateTime(DateTime.UtcNow)
                .AddDays(days * -1)
                .ToDateTime(TimeOnly.MinValue);
        }
    }

    extension(DateTime dateTime)
    {
        public long ToEpochMilliseconds()
        {
            return (long)(dateTime - UnixEpoch).TotalMilliseconds;
        }

        public long ToEpochSeconds()
        {
            return (long)(dateTime - UnixEpoch).TotalSeconds;
        }

        public DateTime Floor(TimeSpan interval)
        {
            return dateTime.AddTicks(-(dateTime.Ticks % interval.Ticks));
        }

        public DateTime Ceiling(TimeSpan interval)
        {
            var overflow = dateTime.Ticks % interval.Ticks;

            return overflow == 0 ? dateTime : dateTime.AddTicks(interval.Ticks - overflow);
        }

        public DateTime Round(TimeSpan interval)
        {
            var halfIntervalTicks = (interval.Ticks + 1) >> 1;

            return dateTime.AddTicks(halfIntervalTicks - (dateTime.Ticks + halfIntervalTicks) % interval.Ticks);
        }
    }
}