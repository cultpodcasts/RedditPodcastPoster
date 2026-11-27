namespace RedditPodcastPoster.Configuration.Services;

public class DateTimeService : IDateTimeService
{
    public DateTime GetUtcNow() => DateTime.UtcNow;

    public int GetHour() => GetUtcNow().Hour;
}
