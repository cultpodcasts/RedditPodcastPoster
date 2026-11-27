namespace Indexer.Services;

public interface IIndexingStrategy
{
    /// <summary>
    /// Schedule for <paramref name="pass"/> at the current UTC time. Throws when that pass
    /// is not one of the two passes that run this hour.
    /// </summary>
    HourlyIndexingSchedule.PassPlan GetPassPlan(int pass);
}
