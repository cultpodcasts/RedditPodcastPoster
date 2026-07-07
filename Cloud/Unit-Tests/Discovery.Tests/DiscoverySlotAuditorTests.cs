using FluentAssertions;
using Microsoft.DurableTask.Client;
using Xunit;

namespace Discovery.Tests;

public class DiscoverySlotAuditorTests
{
    [Fact]
    public void AuditSlot_reports_completed_instance_for_slot()
    {
        var slotStart = new DateTimeOffset(2026, 7, 7, 8, 33, 0, TimeSpan.Zero);
        var createdAt = new DateTimeOffset(2026, 7, 7, 14, 30, 10, TimeSpan.Zero);

        var audit = DiscoverySlotAuditor.AuditSlot(
            slotStart,
            [new DiscoveryOrchestrationInstance(createdAt, OrchestrationRuntimeStatus.Completed, "abc123")]);

        audit.Kind.Should().Be(DiscoverySlotAuditKind.Completed);
        audit.InstanceId.Should().Be("abc123");
    }

    [Fact]
    public void AuditSlot_reports_missing_when_no_instance_for_slot()
    {
        var slotStart = new DateTimeOffset(2026, 7, 7, 8, 33, 0, TimeSpan.Zero);

        var audit = DiscoverySlotAuditor.AuditSlot(slotStart, []);

        audit.Kind.Should().Be(DiscoverySlotAuditKind.Missing);
    }
}
