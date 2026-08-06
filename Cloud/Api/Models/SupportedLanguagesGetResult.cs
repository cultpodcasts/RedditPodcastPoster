using RedditPodcastPoster.Models.Languages;

namespace Api.Models;

public enum SupportedLanguagesGetStatus
{
    Ok,
    Failed
}

public record SupportedLanguagesGetResult(
    SupportedLanguagesGetStatus Status,
    SupportedLanguagesConfig? Config = null,
    bool IsDefault = false);
