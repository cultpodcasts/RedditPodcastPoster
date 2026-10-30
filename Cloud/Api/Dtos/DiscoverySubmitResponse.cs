using System.Text.Json.Serialization;

namespace Api.Dtos;

public class DiscoverySubmitResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("errorsOccurred")]
    public bool ErrorsOccurred { get; set; }

    [JsonPropertyName("results")]
    public required Item[] Results { get; set; }

    [JsonPropertyName("searchIndexerState")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required SearchIndexerState SearchIndexerState { get; set; }

    /// <summary>Items that must be answered before anything is created. Empty results accompany this.</summary>
    [JsonPropertyName("languageDecisionsRequired")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LanguageDecision[]? LanguageDecisionsRequired { get; set; }

    /// <summary>Items whose authority search is ambiguous. Empty results accompany this.</summary>
    [JsonPropertyName("identityDecisionsRequired")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IdentityDecision[]? IdentityDecisionsRequired { get; set; }

    public class LanguageDecision
    {
        [JsonPropertyName("discoveryItemId")]
        public required Guid DiscoveryItemId { get; set; }

        [JsonPropertyName("code")]
        public required string Code { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("signals")]
        public required LanguageSignal[] Signals { get; set; }
    }

    public class LanguageSignal
    {
        [JsonPropertyName("field")]
        public required string Field { get; set; }

        [JsonPropertyName("label")]
        public required string Label { get; set; }

        [JsonPropertyName("excerpt")]
        public required string Excerpt { get; set; }

        [JsonPropertyName("reason")]
        public required string Reason { get; set; }
    }

    public class IdentityDecision
    {
        [JsonPropertyName("discoveryItemId")]
        public required Guid DiscoveryItemId { get; set; }

        [JsonPropertyName("target")]
        public required string Target { get; set; }

        [JsonPropertyName("query")]
        public required string Query { get; set; }

        [JsonPropertyName("candidates")]
        public required IdentityCandidate[] Candidates { get; set; }
    }

    public class IdentityCandidate
    {
        [JsonPropertyName("key")]
        public required string Key { get; set; }

        [JsonPropertyName("title")]
        public required string Title { get; set; }

        [JsonPropertyName("year")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Year { get; set; }

        [JsonPropertyName("kind")]
        public required string Kind { get; set; }

        [JsonPropertyName("source")]
        public required string Source { get; set; }

        [JsonPropertyName("tmdbId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? TmdbId { get; set; }

        [JsonPropertyName("tvdbId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? TvdbId { get; set; }

        [JsonPropertyName("pageUrl")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PageUrl { get; set; }

        [JsonPropertyName("thumbnailUrl")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ThumbnailUrl { get; set; }

        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Description { get; set; }
    }

    public class Item
    {
        [JsonPropertyName("discoveryItemId")]
        public required Guid DiscoveryItemId { get; set; }

        [JsonPropertyName("podcastId")]
        public Guid? PodcastId { get; set; }

        [JsonPropertyName("episodeId")]
        public Guid? EpisodeId { get; set; }

        [JsonPropertyName("message")]
        public required string Message { get; set; }

        /// <summary>Why the item failed (exception type and message). <c>null</c> on success.</summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; }

        /// <summary>
        /// <c>NewsReport</c> or <c>TvShowEpisode</c> when the channel belongs to a news organisation / TV show
        /// (<see cref="EpisodeId"/> / <see cref="PodcastId"/> are then the item and its parent). Omitted for podcasts.
        /// </summary>
        [JsonPropertyName("contentKind")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ContentKind { get; set; }
    }
}
