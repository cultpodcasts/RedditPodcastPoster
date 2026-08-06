using RedditPodcastPoster.Models.Languages;

namespace Api.Models;

public enum SupportedLanguagesUpdateStatus
{
    Ok,
    BadRequest,
    Failed
}

public record SupportedLanguagesUpdateResult(
    SupportedLanguagesUpdateStatus Status,
    SupportedLanguagesConfig? Config = null,
    string? Error = null);
