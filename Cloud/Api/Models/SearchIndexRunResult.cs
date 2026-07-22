using RedditPodcastPoster.Search.Models;

namespace Api.Models;

public enum SearchIndexRunStatus
{
    Ok,
    BadRequest,
    Failed
}

public record SearchIndexRunResult(
    SearchIndexRunStatus Status,
    IndexerStateWrapper? Result = null);
