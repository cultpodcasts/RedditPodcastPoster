namespace RedditPodcastPoster.Configuration.Services;

public interface IDateTimeService
{
    int GetHour();

    /// <summary>Current UTC clock for schedules that need hour and day-of-year.</summary>
    DateTime GetUtcNow();
}
