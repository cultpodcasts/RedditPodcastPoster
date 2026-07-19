using FluentAssertions;
using Microsoft.DurableTask.Client;
using Xunit;

namespace Discovery.Tests;

public class DiscoverySlotAuditorTests
{
    private static readonly IReadOnlyList<TimeOnly> RunTimes =
        DiscoverySchedule.ParseRunTimes(["08:00", "22:00"]);

    private static readonly TimeZoneInfo Uk = DiscoverySchedule.ResolveUkTimeZone();

    [Fact]
    public void AuditSlot_reports_completed_instance_for_slot()
    {
        // 08:00 UK BST = 07:00 UTC on 2026-07-19
        var slot = DiscoverySchedule.TryMatchDueSlot(
            DateTime.Parse("2026-07-19T07:00:00Z").ToUniversalTime(), RunTimes, null, Uk)!.Value;
        var createdAt = new DateTimeOffset(2026, 7, 19, 7, 1, 0, TimeSpan.Zero);

        var audit = DiscoverySlotAuditor.AuditSlot(
            slot,
            slot.SlotStartUtc,
            [new DiscoveryOrchestrationInstance(createdAt, OrchestrationRuntimeStatus.Completed, "abc123")],
            RunTimes,
            Uk);

        audit.Kind.Should().Be(DiscoverySlotAuditKind.Completed);
        audit.InstanceId.Should().Be("abc123");
        audit.SlotId.Should().Be("2026-07-19 08:00 UK");
    }

    [Fact]
    public void AuditSlot_reports_missing_when_no_instance_for_slot()
    {
        var slot = DiscoverySchedule.TryMatchDueSlot(
            DateTime.Parse("2026-07-19T07:00:00Z").ToUniversalTime(), RunTimes, null, Uk)!.Value;

        var audit = DiscoverySlotAuditor.AuditSlot(slot, slot.SlotStartUtc, [], RunTimes, Uk);

        audit.Kind.Should().Be(DiscoverySlotAuditKind.Missing);
        audit.SlotId.Should().Be(slot.SlotId);
    }
}
