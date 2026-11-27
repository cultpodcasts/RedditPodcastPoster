namespace Indexer.Services;

public class IndexingStrategy : IIndexingStrategy
{
    public HourlyIndexingSchedule.PassPlan GetPassPlan(DateTime scheduleUtc, int pass)
    {
        var plan = HourlyIndexingSchedule.ForPass(scheduleUtc, pass);
        if (plan is null)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pass),
                $"Pass {pass} is not scheduled at UTC hour {scheduleUtc.Hour}.");
        }

        return plan;
    }
}
