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
    TitleCasingRulesDocument? Document = null,
    string? Error = null);
