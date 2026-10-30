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

/// <summary>400 body for an index that was refused: <c>NotAutoIndexed</c> or <c>SourcePodcastStillExists</c>.</summary>
public class CatalogueIndexRejection
{
    public const string NotAutoIndexed = "NotAutoIndexed";
    public const string SourcePodcastStillExists = "SourcePodcastStillExists";

    [JsonPropertyName("reason")]
    public required string Reason { get; init; }
}
