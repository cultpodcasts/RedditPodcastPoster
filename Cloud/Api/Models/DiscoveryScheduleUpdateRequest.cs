using System.Text.Json.Serialization;

namespace Api.Models;

public class DiscoveryScheduleUpdateRequest
{
    [JsonPropertyName("runTimes")]
    public required List<string> RunTimes { get; init; }

    [JsonPropertyName("timeZoneId")]
    public string? TimeZoneId { get; init; }

    [JsonPropertyName("enabled")]
    public bool? Enabled { get; init; }
}
