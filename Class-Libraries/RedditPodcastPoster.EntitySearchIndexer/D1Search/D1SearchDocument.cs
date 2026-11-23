using System.Text.Json.Serialization;

namespace RedditPodcastPoster.EntitySearchIndexer.D1Search;

/// <summary>
/// One document for the D1 search index write route (Api <c>searchIndexDocSchema</c>, <c>POST /search/admin/docs</c>).
/// The Azure document (<c>EpisodeSearchRecord</c>) plus the fields Azure never had: the <b>full</b> description (Azure
/// keeps ~100 characters), <c>guests</c> (SP-0.3: both from soak day one) and <c>modifiedAt</c> for the Worker's
/// stale-replay guard. The Worker rejects unknown fields with a 400, so this type is the whole contract; the three
/// legacy Azure names (<c>podcastName</c>, <c>episodeTitle</c>, <c>episodeDescription</c>) are never sent.
/// </summary>
public sealed class D1SearchDocument
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("contentKind")] public string? ContentKind { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("seriesName")] public string? SeriesName { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }

    /// <summary>ISO-8601 with a zone; the Worker stores it as UTC.</summary>
    [JsonPropertyName("release")] public required DateTimeOffset Release { get; init; }

    /// <summary>.NET <see cref="TimeSpan"/> string, as Azure has it.</summary>
    [JsonPropertyName("duration")] public required string Duration { get; init; }

    [JsonPropertyName("spotifyId")] public string? SpotifyId { get; init; }
    [JsonPropertyName("appleId")] public string? AppleId { get; init; }
    [JsonPropertyName("podcastAppleId")] public string? PodcastAppleId { get; init; }
    [JsonPropertyName("youtubeId")] public string? YoutubeId { get; init; }
    [JsonPropertyName("bbc")] public string? Bbc { get; init; }
    [JsonPropertyName("internetArchive")] public string? InternetArchive { get; init; }
    [JsonPropertyName("svc")] public string? Svc { get; init; }
    [JsonPropertyName("subjects")] public string[]? Subjects { get; init; }
    [JsonPropertyName("image")] public string? Image { get; init; }
    [JsonPropertyName("trailers")] public string[]? Trailers { get; init; }
    [JsonPropertyName("youTubeAlternates")] public string[]? YouTubeAlternates { get; init; }
    [JsonPropertyName("alternativeTitles")] public string[]? AlternativeTitles { get; init; }
    [JsonPropertyName("imdbId")] public string? ImdbId { get; init; }
    [JsonPropertyName("tmdbId")] public int? TmdbId { get; init; }
    [JsonPropertyName("tvdbId")] public long? TvdbId { get; init; }
    [JsonPropertyName("seriesImdbId")] public string? SeriesImdbId { get; init; }
    [JsonPropertyName("seriesTmdbId")] public int? SeriesTmdbId { get; init; }
    [JsonPropertyName("seriesTvdbId")] public long? SeriesTvdbId { get; init; }
    [JsonPropertyName("lang")] public string? Lang { get; init; }

    /// <summary>Searchable, never returned (SP-0.3).</summary>
    [JsonPropertyName("guests")] public string[]? Guests { get; init; }

    [JsonPropertyName("publisherSearchTerms")] public string? PublisherSearchTerms { get; init; }
    [JsonPropertyName("episodeSearchTerms")] public string? EpisodeSearchTerms { get; init; }

    /// <summary>
    /// Source change time, epoch milliseconds: the playable's Cosmos <c>_ts</c> × 1000. The Worker drops an upsert
    /// older than the stored document (listed as <c>stale</c>) and applies an equal one (a retry), so a replayed or
    /// reordered write never overwrites newer data.
    /// </summary>
    [JsonPropertyName("modifiedAt")] public required long ModifiedAt { get; init; }
}
