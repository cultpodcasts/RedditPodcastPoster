namespace RedditPodcastPoster.Configuration.Extensions;

public static class DateTimeExtensions
{
    private static readonly DateTime UnixEpoch = new(1970, 1, 1);

    public static long ToEpochMilliseconds(this DateTime dateTime)
    {
        return (long)(dateTime - UnixEpoch).TotalMilliseconds;
    }

    public static long ToEpochSeconds(this DateTime dateTime)
    {
        return (long)(dateTime - UnixEpoch).TotalSeconds;
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