using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.Indexing.Models;

namespace Api.Models;

public enum PodcastIndexStatus
{
    Ok,
    NotFound,
    BadRequest,
    Failed,

    /// <summary>Another index already holds this podcast's lease (HTTP 409).</summary>
    AlreadyRunning
}

public record PodcastIndexResult(
    PodcastIndexStatus Status,
    IndexResponse? IndexResponse = null,
    EntitySearchIndexerResponse? SearchIndexer = null);
