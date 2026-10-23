namespace Api.Models;

public enum DiscoveryChannelBanStatus
{
    Accepted,
    NotFound,
    BadRequest,
    Conflict,
    Failed
}

/// <summary>What the ban command did. Not the saved podcast.</summary>
public sealed record DiscoveryChannelBanResult(DiscoveryChannelBanStatus Status, string? Error = null);
