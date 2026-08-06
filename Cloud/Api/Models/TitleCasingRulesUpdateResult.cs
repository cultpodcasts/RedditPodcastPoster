using RedditPodcastPoster.Models.TitleCasing;

namespace Api.Models;

public enum TitleCasingRulesUpdateStatus
{
    Ok,
    BadRequest,
    Failed
}

public record TitleCasingRulesUpdateResult(
    TitleCasingRulesUpdateStatus Status,
    LanguageTitleCasingRulesDocument? Document = null,
    string? Error = null);
