namespace Indexer.Activities;

/// <param name="ScheduleUtc">
/// Orchestration clock for this pass (replay-safe <c>CurrentUtcDateTime</c>).
/// Frozen so the activity does not re-read wall clock after the hour rolls.
/// </param>
public record IndexerContextWrapper(IndexerContext IndexerContext, int Pass, DateTime ScheduleUtc);
