using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Services;

namespace RedditPodcastPoster.Models.TvShows;

/// <summary>
/// Playable TV episode. Platform presence is <see cref="Services"/> only —
/// no Spotify/Apple/YouTube provider-id fields (those exist on podcast episodes for collection matching).
/// Release is a calendar date (no time-of-day).
/// </summary>
[CosmosSelector(ModelType.TvShowEpisode)]
public sealed class TvShowEpisode : CosmosSelector
{
    public TvShowEpisode()
    {
        Id = Guid.NewGuid();
        ModelType = ModelType.TvShowEpisode;
    }

    public TvShowEpisode(string title) : this()
    {
        Title = title;
    }

    [JsonPropertyName("tvShowId")]
    [JsonPropertyOrder(3)]
    public Guid TvShowId { get; set; }

    [JsonPropertyName("title")]
    [JsonPropertyOrder(10)]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonPropertyOrder(20)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Calendar date only — not a podcast-episode UTC datetime. Use <see cref="CatalogueRelease.FromDate"/>.</summary>
    [JsonPropertyName("release")]
    [JsonPropertyOrder(30)]
    public CatalogueRelease? Release { get; set; }

    [JsonPropertyName("duration")]
    [JsonPropertyOrder(31)]
    public TimeSpan Length { get; set; }

    [JsonPropertyName("explicit")]
    [JsonPropertyOrder(32)]
    public bool Explicit { get; set; }

    [JsonPropertyName("lang")]
    [JsonPropertyOrder(45)]
    public string? Language { get; set; }

    [JsonPropertyName("ignored")]
    [JsonPropertyOrder(43)]
    public bool Ignored { get; set; }

    [JsonPropertyName("removed")]
    [JsonPropertyOrder(44)]
    public bool Removed { get; set; }

    [JsonPropertyName("subjects")]
    [JsonPropertyOrder(70)]
    public List<string> Subjects { get; set; } = [];

    [JsonPropertyName("removedSubjects")]
    [JsonPropertyOrder(71)]
    public List<string> RemovedSubjects { get; set; } = [];

    [JsonPropertyName("searchTerms")]
    [JsonPropertyOrder(80)]
    public string? SearchTerms { get; set; }

    [JsonPropertyName("tvShowName")]
    [JsonPropertyOrder(90)]
    public string? TvShowName { get; set; }

    [JsonPropertyName("tvShowSearchTerms")]
    [JsonPropertyOrder(91)]
    public string? TvShowSearchTerms { get; set; }

    [JsonPropertyName("tvShowLanguage")]
    [JsonPropertyOrder(92)]
    public string? TvShowLanguage { get; set; }

    [JsonPropertyName("tvShowMetadataVersion")]
    [JsonPropertyOrder(93)]
    public long? TvShowMetadataVersion { get; set; }

    [JsonPropertyName("tvShowRemoved")]
    [JsonPropertyOrder(94)]
    public bool? TvShowRemoved { get; set; }

    [JsonPropertyName("services")]
    [JsonPropertyOrder(151)]
    public Dictionary<string, ServiceLink>? Services { get; set; }

    [JsonPropertyName("guests")]
    [JsonPropertyOrder(160)]
    public string[]? Guests { get; set; }

    /// <summary>
    /// Denormalise parent TvShow fields onto this playable (mirrors
    /// <c>Episode.SetPodcastProperties</c>). First flag = projection fields;
    /// second = parent <c>_ts</c> / metadata version only.
    /// </summary>
    public (bool Updated, bool UpdatedMetadata) SetTvShowProperties(TvShow tvShow)
    {
        var updated = false;
        if (TvShowId != tvShow.Id)
        {
            TvShowId = tvShow.Id;
            updated = true;
        }

        var name = tvShow.Name.Trim();
        if (TvShowName != name)
        {
            TvShowName = name;
            updated = true;
        }

        if (TvShowRemoved != tvShow.Removed)
        {
            TvShowRemoved = tvShow.Removed;
            updated = true;
        }

        var searchTerms = tvShow.SearchTerms?.Trim();
        if (TvShowSearchTerms != searchTerms)
        {
            TvShowSearchTerms = searchTerms;
            updated = true;
        }

        var language = tvShow.Language?.Trim();
        if (TvShowLanguage != language)
        {
            TvShowLanguage = language;
            updated = true;
        }

        var updatedMetadata = false;
        if (TvShowMetadataVersion != tvShow.Timestamp)
        {
            TvShowMetadataVersion = tvShow.Timestamp;
            updatedMetadata = true;
        }

        return (updated, updatedMetadata);
    }
}
