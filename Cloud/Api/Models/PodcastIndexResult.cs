using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.Indexing.Models;

namespace Api.Models;

public enum PodcastIndexStatus
{
    Ok,
    NotFound,
    BadRequest,
    Failed
}

public record PodcastIndexResult(
    PodcastIndexStatus Status,
    IndexResponse? IndexResponse = null,
    EntitySearchIndexerResponse? SearchIndexer = null);
