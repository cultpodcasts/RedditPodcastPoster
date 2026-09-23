using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Shared catalogue publisher (series / organisation / standalone work) fields:
/// name, description, social handles, subject defaults, and indexing bookmarks.
/// <see cref="Podcasts.Podcast"/>, <see cref="TvShows.TvShow"/>,
/// <see cref="News.NewsOrganisation"/>, and <see cref="Films.Film"/> subclass this.
/// Film uses <see cref="Name"/> as its display title (no separate <c>title</c> member).
/// </summary>
public abstract class Publisher : CosmosSelector, ICatalogueCopy, IRemovable
{
    [JsonPropertyName("name")]
    [JsonPropertyOrder(20)]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonPropertyOrder(21)]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("latestReleased")]
    [JsonPropertyOrder(22)]
    public DateTime? LatestReleased { get; set; }

    [JsonPropertyName("lang")]
    [JsonPropertyOrder(23)]
    public string? Language { get; set; }

    [JsonPropertyName("lastIndexed")]
    [JsonPropertyOrder(24)]
    public DateTime? LastIndexed { get; set; }

    [JsonPropertyName("removed")]
    [JsonPropertyOrder(25)]
    public bool? Removed { get; set; }

    /// <summary>Network / label / brand attribution (JSON <c>publisher</c>).</summary>
    [JsonPropertyName("publisher")]
    [JsonPropertyOrder(30)]
    public string PublisherName { get; set; } = string.Empty;

    [JsonPropertyName("twitterHandle")]
    [JsonPropertyOrder(190)]
    public string TwitterHandle { get; set; } = string.Empty;

    [JsonPropertyName("blueskyHandle")]
    [JsonPropertyOrder(191)]
    public string? BlueskyHandle { get; set; }

    [JsonPropertyName("hashtag")]
    [JsonPropertyOrder(195)]
    public string? HashTag { get; set; }

    [JsonPropertyName("enrichmentHashTags")]
    [JsonPropertyOrder(196)]
    public string[]? EnrichmentHashTags { get; set; }

    [JsonPropertyName("ignoredAssociatedSubjects")]
    [JsonPropertyOrder(240)]
    public string[]? IgnoredAssociatedSubjects { get; set; }

    [JsonPropertyName("ignoredSubjects")]
    [JsonPropertyOrder(250)]
    public string[]? IgnoredSubjects { get; set; }

    [JsonPropertyName("defaultSubject")]
    [JsonPropertyOrder(260)]
    public string? DefaultSubject { get; set; }

    [JsonPropertyName("searchTerms")]
    [JsonPropertyOrder(270)]
    public string? SearchTerms { get; set; }

    [JsonPropertyName("knownTerms")]
    [JsonPropertyOrder(271)]
    public string[]? KnownTerms { get; set; }

    public bool IsRemoved() => Removed == true;
}
