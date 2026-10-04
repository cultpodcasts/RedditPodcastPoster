using FluentAssertions;
using Azure;
using Xunit;

namespace FunctionHost.Tests.BusinessRules.Telemetry;

public class AzureMonitorExporterPolicyRules
{
    [Fact(DisplayName =
        "Azure Monitor must not sample AppTraces with the request span, " +
        "because a 5xx Isolated invoke can land in AppRequests with no failure log " +
        "(2026-10-04 POST SubmitUrl 500).")]
    public void trace_based_log_sampler_is_off()
    {
        // Arrange
        // Act
        var samplerEnabled = AzureMonitorExporterPolicy.EnableTraceBasedLogsSampler;

        // Assert
        samplerEnabled.Should().BeFalse();
    }
}
