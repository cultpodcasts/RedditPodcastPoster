namespace Api.Models;

public class DiscoveryScheduleUpdateRequest
{
    public required List<string> RunTimes { get; init; }

    public string? TimeZoneId { get; init; }

    public bool? Enabled { get; init; }
}
