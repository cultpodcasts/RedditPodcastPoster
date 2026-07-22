using RedditPodcastPoster.EntitySearchIndexer.Models;

namespace Api.Models;

public enum PodcastRenameStatus
{
    Ok,
    Conflict,
    NotFound,
    BadRequest,
    InvalidName,
    TooMany,
    Failed
}

public record PodcastRenameResult(
    PodcastRenameStatus Status,
    EntitySearchIndexerResponse? SearchIndexer = null);
