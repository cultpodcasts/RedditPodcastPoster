using RedditPodcastPoster.Models.TitleCasing;

namespace Api.Models;

public enum TitleCasingRulesGetStatus
{
    Ok,
    NotFound,
    Failed
}

public record TitleCasingRulesGetResult(
    TitleCasingRulesGetStatus Status,
    LanguageTitleCasingRulesDocument? Document = null,
    bool IsDefault = false);
