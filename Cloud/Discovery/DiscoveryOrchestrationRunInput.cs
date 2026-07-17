namespace Discovery;

/// <summary>
/// Input for <see cref="Orchestration" /> carrying the UTC time the trigger scheduled the run,
/// so the orchestration can no-op if it executes in a later discovery slot (e.g. a Pending backlog
/// draining after a broken host is fixed).
/// </summary>
public record DiscoveryOrchestrationRunInput(DateTime ScheduledAtUtc);
