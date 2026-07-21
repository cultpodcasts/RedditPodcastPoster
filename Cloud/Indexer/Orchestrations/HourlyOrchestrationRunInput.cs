namespace Indexer.Orchestrations;

/// <summary>
/// Input for <see cref="HourlyOrchestration" /> carrying the UTC time the trigger scheduled the run,
/// so the orchestration can no-op if it executes in a later UTC hour (e.g. a Pending backlog draining
/// after a broken host is fixed — Jul 2026 duplicate Bluesky posts incident).
/// </summary>
public record HourlyOrchestrationRunInput(DateTime ScheduledAtUtc);
