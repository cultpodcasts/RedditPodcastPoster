namespace Api.Models;

public enum SupportedLanguagesUpdateStatus
{
    Ok,
    BadRequest,
    Failed
}

public record SupportedLanguagesUpdateResult(
    SupportedLanguagesUpdateStatus Status,
    string? Error = null);
