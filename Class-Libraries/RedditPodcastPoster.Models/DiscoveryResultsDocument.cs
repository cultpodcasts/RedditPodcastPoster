using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

[CosmosSelector(ModelType.Discovery)]
public sealed class DiscoveryResultsDocument : CosmosSelector
{
    public DiscoveryResultsDocument(
        DateTime discoveryBegan,
        IEnumerable<DiscoveryResult> discoveryResults)
    {
        Id = Guid.NewGuid();
        ModelType = ModelType.Discovery;
        DiscoveryBegan = discoveryBegan;
        DiscoveryResults = discoveryResults;
        State = DiscoveryResultsDocumentState.Unprocessed;
    }

    [JsonPropertyName("state")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    [JsonPropertyOrder(10)]
    public DiscoveryResultsDocumentState State { get; set; }

    [JsonPropertyName("discoveryBegan")]
    [JsonPropertyOrder(20)]
    public DateTime DiscoveryBegan { get; set; }

    [JsonPropertyName("discoveryResults")]
    [JsonPropertyOrder(500)]
    public IEnumerable<DiscoveryResult> DiscoveryResults { get; set; }

    [JsonPropertyName("excludeSpotify")]
    [JsonPropertyOrder(30)]
    public bool ExcludeSpotify { get; set; }

    [JsonPropertyName("includeYouTube")]
    [JsonPropertyOrder(31)]
    public bool IncludeYouTube { get; set; }

    [JsonPropertyName("includeListenNotes")]
    [JsonPropertyOrder(32)]
    public bool IncludeListenNotes { get; set; }

    [JsonPropertyName("enrichListenNotesFromSpotify")]
    [JsonPropertyOrder(33)]
    public bool EnrichListenNotesFromSpotify { get; set; }

    [JsonPropertyName("searchSince")]
    [JsonPropertyOrder(34)]
    public required string SearchSince { get; set; }

    [JsonPropertyName("preSkipSpotifyUrlResolving")]
    [JsonPropertyOrder(35)]
    public bool PreSkipSpotifyUrlResolving { get; set; }

    [JsonPropertyName("postSkipSpotifyUrlResolving")]
    [JsonPropertyOrder(36)]
    public bool PostSkipSpotifyUrlResolving { get; set; }
}