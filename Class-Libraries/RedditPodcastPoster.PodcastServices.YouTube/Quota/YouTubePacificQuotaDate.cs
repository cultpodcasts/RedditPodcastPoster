namespace RedditPodcastPoster.PodcastServices.YouTube.Quota;

public static class YouTubePacificQuotaDate
{
    private static readonly TimeZoneInfo PacificTimeZone = ResolvePacificTimeZone();

    public static DateOnly GetCurrent(DateTime utcNow)
    {
        var pacificNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, PacificTimeZone);
        return DateOnly.FromDateTime(pacificNow);
    }

    private static TimeZoneInfo ResolvePacificTimeZone()
    {
        foreach (var timeZoneId in new[] { "America/Los_Angeles", "Pacific Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        throw new InvalidOperationException("Unable to resolve Pacific time zone.");
    }
}
