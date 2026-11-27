namespace Indexer.Services;

public interface IIndexingStrategy
{
    /// <summary>
    /// Schedule for <paramref name="pass"/> at <paramref name="scheduleUtc"/>. Throws when that
    /// pass is not one of the two passes that run in that UTC hour.
    /// </summary>
    HourlyIndexingSchedule.PassPlan GetPassPlan(DateTime scheduleUtc, int pass);
}
