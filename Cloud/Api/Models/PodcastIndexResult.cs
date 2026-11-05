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
    AlreadyRunning,

    /// <summary>The on-demand time budget cut the index off (HTTP 504).</summary>
    TimedOut
}

public record PodcastIndexResult(
    PodcastIndexStatus Status,
    IndexResponse? IndexResponse = null,
    EntitySearchIndexerResponse? SearchIndexer = null);
