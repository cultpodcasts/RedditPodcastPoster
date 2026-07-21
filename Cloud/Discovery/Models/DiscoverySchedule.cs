namespace Discovery.Models;

/// <summary>
/// Compatibility aliases so Cloud Discovery code and tests keep a short name.
/// Implementation lives in <see cref="RedditPodcastPoster.Discovery.Scheduling.DiscoverySchedule"/>.
/// </summary>
public readonly record struct DiscoverySlot(
    string SlotId,
    DateTimeOffset SlotStartUtc,
    DateTimeOffset SlotStartUk,
    TimeOnly RunTimeUk)
{
    public static implicit operator DiscoverySlot(RedditPodcastPoster.Discovery.Scheduling.DiscoverySlot slot) =>
        new(slot.SlotId, slot.SlotStartUtc, slot.SlotStartUk, slot.RunTimeUk);

    public static implicit operator RedditPodcastPoster.Discovery.Scheduling.DiscoverySlot(DiscoverySlot slot) =>
        new(slot.SlotId, slot.SlotStartUtc, slot.SlotStartUk, slot.RunTimeUk);
}

public static class DiscoverySchedule
{
    public static readonly TimeSpan DefaultGrace =
        RedditPodcastPoster.Discovery.Scheduling.DiscoverySchedule.DefaultGrace;

    public static readonly IReadOnlyList<TimeOnly> DefaultRunTimesUk =
        RedditPodcastPoster.Discovery.Scheduling.DiscoverySchedule.DefaultRunTimesUk;

    public static TimeZoneInfo ResolveUkTimeZone(string? configuredTimeZoneId = null) =>
        RedditPodcastPoster.Discovery.Scheduling.DiscoverySchedule.ResolveUkTimeZone(configuredTimeZoneId);

    public static IReadOnlyList<TimeOnly> ParseRunTimes(IEnumerable<string>? runTimes) =>
        RedditPodcastPoster.Discovery.Scheduling.DiscoverySchedule.ParseRunTimes(runTimes);

    public static DiscoverySlot? TryMatchDueSlot(
        DateTime utcNow,
        IReadOnlyList<TimeOnly> runTimesUk,
        TimeSpan? grace = null,
        TimeZoneInfo? ukTimeZone = null)
    {
        var match = RedditPodcastPoster.Discovery.Scheduling.DiscoverySchedule.TryMatchDueSlot(
            utcNow, runTimesUk, grace, ukTimeZone);
        return match is null ? null : (DiscoverySlot)match.Value;
    }

    public static DiscoverySlot GetLatestDueSlot(
        DateTime utcNow,
        IReadOnlyList<TimeOnly> runTimesUk,
        TimeZoneInfo? ukTimeZone = null) =>
        RedditPodcastPoster.Discovery.Scheduling.DiscoverySchedule.GetLatestDueSlot(utcNow, runTimesUk, ukTimeZone);

    public static DiscoverySlot GetPriorSlot(
        DiscoverySlot current,
        IReadOnlyList<TimeOnly> runTimesUk,
        TimeZoneInfo? ukTimeZone = null) =>
        RedditPodcastPoster.Discovery.Scheduling.DiscoverySchedule.GetPriorSlot(current, runTimesUk, ukTimeZone);

    public static bool IsStaleRun(
        DateTimeOffset scheduledSlotStartUtc,
        DateTime currentUtcDateTime,
        IReadOnlyList<TimeOnly> runTimesUk,
        TimeZoneInfo? ukTimeZone = null) =>
        RedditPodcastPoster.Discovery.Scheduling.DiscoverySchedule.IsStaleRun(
            scheduledSlotStartUtc, currentUtcDateTime, runTimesUk, ukTimeZone);

    public static IReadOnlyList<DiscoverySlot> PreviewNextRuns(
        DateTime utcNow,
        IReadOnlyList<TimeOnly> runTimesUk,
        int count,
        TimeZoneInfo? ukTimeZone = null) =>
        RedditPodcastPoster.Discovery.Scheduling.DiscoverySchedule
            .PreviewNextRuns(utcNow, runTimesUk, count, ukTimeZone)
            .Select(s => (DiscoverySlot)s)
            .ToList();

    public static string FormatSlot(DiscoverySlot slot) =>
        RedditPodcastPoster.Discovery.Scheduling.DiscoverySchedule.FormatSlot(slot);
}
