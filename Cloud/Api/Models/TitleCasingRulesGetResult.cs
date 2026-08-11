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
    TitleCasingRulesDocument? Document = null,
    bool IsDefault = false);
