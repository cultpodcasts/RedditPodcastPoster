namespace Api.Models;

public enum DiscoveryCurationGetStatus
{
    Ok,
    Failed
}

public record DiscoveryCurationGetResult(
    DiscoveryCurationGetStatus Status,
    DiscoveryCurationData? Data = null);
