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
}

/// <summary>
/// Body of an index that did not complete: 400 with <c>NotAutoIndexed</c> or <c>SourcePodcastStillExists</c> (refused),
/// or 504 with <c>TimedOut</c> (the on-demand time budget cut it off; <c>message</c> says what that means).
/// </summary>
public class CatalogueIndexRejection
{
    public const string NotAutoIndexed = "NotAutoIndexed";
    public const string SourcePodcastStillExists = "SourcePodcastStillExists";
    public const string TimedOut = "TimedOut";

    /// <summary>The 504 message. It makes no claim about what was saved, because that is not known at the cut.</summary>
    public const string TimedOutMessage =
        "Indexing ran out of time; anything it saved will reach search on the next pull-indexer run. Try again later.";

    [JsonPropertyName("reason")]
    public required string Reason { get; init; }

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }
}
