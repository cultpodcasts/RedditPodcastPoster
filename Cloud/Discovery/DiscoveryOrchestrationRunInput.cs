namespace Discovery;

public record DiscoveryOrchestrationRunInput(
    DateTime ScheduledAtUtc,
    DateTimeOffset SlotStartUtc,
    string SlotId,
    string[] RunTimesUk);
