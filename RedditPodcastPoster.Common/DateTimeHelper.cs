namespace RedditPodcastPoster.Common;

public static class DateTimeHelper
{
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