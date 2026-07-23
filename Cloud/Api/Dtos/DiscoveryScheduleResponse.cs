using System.Text.Json.Serialization;

namespace Api.Dtos;

public class DiscoveryScheduleResponse
{
    [JsonPropertyName("runTimes")]
    public required IReadOnlyList<string> RunTimes { get; init; }

    [JsonPropertyName("timeZoneId")]
    public required string TimeZoneId { get; init; }

    [JsonPropertyName("enabled")]
    public required bool Enabled { get; init; }

    [JsonPropertyName("isDefault")]
    public required bool IsDefault { get; init; }

    [JsonPropertyName("nextRuns")]
    public required IReadOnlyList<NextRun> NextRuns { get; init; }

    public class NextRun
    {
        [JsonPropertyName("slotId")]
        public required string SlotId { get; init; }

        [JsonPropertyName("slotStartUtc")]
        public required DateTimeOffset SlotStartUtc { get; init; }

        [JsonPropertyName("slotStartUk")]
        public required DateTimeOffset SlotStartUk { get; init; }
    }
}
