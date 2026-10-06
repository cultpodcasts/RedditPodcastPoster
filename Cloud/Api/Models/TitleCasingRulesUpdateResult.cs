namespace Api.Models;

public enum TitleCasingRulesUpdateStatus
{
    Ok,
    BadRequest,
    Failed
}

public record TitleCasingRulesUpdateResult(
    TitleCasingRulesUpdateStatus Status,
    string? Error = null);
