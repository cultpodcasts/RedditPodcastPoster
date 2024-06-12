namespace RedditPodcastPoster.Configuration;

public class DateTimeService : IDateTimeService
{
    public int GetHour()
    {
        return DateTime.UtcNow.Hour;
    }
}