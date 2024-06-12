namespace RedditPodcastPoster.PodcastServices.YouTube;

public class DateTimeService : IDateTimeService
{
    public int GetHour()
    {
        return DateTime.UtcNow.Hour;
    }
}