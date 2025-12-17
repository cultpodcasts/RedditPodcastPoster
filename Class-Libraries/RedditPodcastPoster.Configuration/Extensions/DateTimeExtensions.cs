namespace RedditPodcastPoster.Configuration.Extensions;

public static class DateTimeExtensions
{
    private static readonly DateTime UnixEpoch = new(1970, 1, 1);

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
    }

    public static DateTime DaysAgo(int days)
    {
        {
            return DateOnly
                .FromDateTime(DateTime.UtcNow)
                .AddDays(days * -1)
                .ToDateTime(TimeOnly.MinValue);
        }
    }
}