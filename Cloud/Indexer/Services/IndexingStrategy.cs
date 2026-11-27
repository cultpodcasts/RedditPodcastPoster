using RedditPodcastPoster.Configuration.Services;

namespace Indexer.Services;

public class IndexingStrategy(IDateTimeService dateTimeService) : IIndexingStrategy
{
    public HourlyIndexingSchedule.PassPlan GetPassPlan(int pass)
    {
        var plan = HourlyIndexingSchedule.ForPass(dateTimeService.GetUtcNow(), pass);
        if (plan is null)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pass),
                $"Pass {pass} is not scheduled at UTC hour {dateTimeService.GetHour()}.");
        }

        return plan;
    }
}
