using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>
/// Command outcome of a curator's on-demand index of a TV show or news organisation (not the saved items: the client
/// re-reads the hub page). <c>indexedItemIds</c> are the TV episodes / news reports the index saved.
/// </summary>
public class CatalogueIndexResponse
{
    [JsonPropertyName("indexedItemIds")]
    public required Guid[] IndexedItemIds { get; init; }

    [JsonPropertyName("searchIndexerState")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SearchIndexerState SearchIndexerState { get; init; }

    /// <summary>
    /// True when the parent is not auto-indexed, so the index only filled in missing Spotify / Apple links (#1400);
    /// <c>indexedItemIds</c> are then the enriched items.
    /// </summary>
    [JsonPropertyName("enrichOnly")]
    public bool EnrichOnly { get; init; }
}

/// <summary>
/// Body of an index that did not complete: 400 with <c>Removed</c>, <c>NotAutoIndexed</c> (not auto-indexed and no
/// Spotify / Apple id to enrich from) or <c>SourcePodcastStillExists</c> (refused), 400 with <c>NothingToEnrich</c> (an
/// enrich-only pass ran but no recent item gained a Spotify / Apple link),
/// 409 with <c>indexingInProgress</c> (another index holds the lease), or 504 with <c>TimedOut</c> (the on-demand time
/// budget cut it off; <c>message</c> says what that means).
/// </summary>
public class CatalogueIndexRejection
{
    public const string Removed = "Removed";
    public const string NotAutoIndexed = "NotAutoIndexed";
    public const string NothingToEnrich = "NothingToEnrich";
    public const string SourcePodcastStillExists = "SourcePodcastStillExists";
    public const string TimedOut = "TimedOut";
    public const string IndexingInProgress = "indexingInProgress";

    /// <summary>The 504 message. It makes no claim about what was saved, because that is not known at the cut.</summary>
    public const string TimedOutMessage =
        "Indexing ran out of time; anything it saved will reach search on the next pull-indexer run. Try again later.";

    public const string RemovedMessage = "It is removed, so it is not indexed.";

    public const string NotAutoIndexedMessage =
        "It is not auto-indexed and has no Spotify or Apple id, so there is nothing to index or enrich.";

    public const string NothingToEnrichMessage =
        "It is not auto-indexed, so only Spotify and Apple links were looked up, and no recent item gained one.";

    public const string IndexingInProgressMessage =
        "Another index of this parent is already running. Try again shortly.";

    [JsonPropertyName("reason")]
    public required string Reason { get; init; }

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }
}
