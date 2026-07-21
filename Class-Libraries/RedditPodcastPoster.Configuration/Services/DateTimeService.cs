namespace RedditPodcastPoster.Configuration.Services;

public class DateTimeService : IDateTimeService
{
    public int GetHour()
    {
        return DateTime.UtcNow.Hour;
    }
}
