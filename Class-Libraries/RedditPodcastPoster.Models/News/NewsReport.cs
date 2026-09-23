using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.News;

/// <summary>
/// Playable news report under a <see cref="NewsOrganisation"/>.
/// Platform presence is <see cref="Playable.Services"/> only — no provider-id fields.
/// Release is a calendar date (no time-of-day).
/// </summary>
[CosmosSelector(ModelType.NewsReport)]
public sealed class NewsReport : Playable
{
    public NewsReport()
    {
        Id = Guid.NewGuid();
        ModelType = ModelType.NewsReport;
    }

    public NewsReport(string title) : this()
    {
        Title = title;
    }

    [JsonPropertyName("newsOrganisationId")]
    [JsonPropertyOrder(3)]
    public Guid NewsOrganisationId { get; set; }

    /// <summary>Calendar date only — not a podcast-episode UTC datetime. Use <see cref="CatalogueRelease.FromDate"/>.</summary>
    [JsonPropertyName("release")]
    [JsonPropertyOrder(30)]
    public CatalogueRelease? Release { get; set; }

    [JsonPropertyName("newsOrganisationName")]
    [JsonPropertyOrder(90)]
    public string? NewsOrganisationName { get; set; }

    [JsonPropertyName("newsOrganisationSearchTerms")]
    [JsonPropertyOrder(91)]
    public string? NewsOrganisationSearchTerms { get; set; }

    [JsonPropertyName("newsOrganisationLanguage")]
    [JsonPropertyOrder(92)]
    public string? NewsOrganisationLanguage { get; set; }

    [JsonPropertyName("newsOrganisationMetadataVersion")]
    [JsonPropertyOrder(93)]
    public long? NewsOrganisationMetadataVersion { get; set; }

    [JsonPropertyName("newsOrganisationRemoved")]
    [JsonPropertyOrder(94)]
    public bool? NewsOrganisationRemoved { get; set; }

    /// <summary>
    /// Denormalise parent NewsOrganisation fields onto this playable (mirrors
    /// <c>Episode.SetPodcastProperties</c> / <c>TvShowEpisode.SetTvShowProperties</c>).
    /// First flag = projection fields; second = parent <c>_ts</c> / metadata version only.
    /// </summary>
    public (bool Updated, bool UpdatedMetadata) SetNewsOrganisationProperties(NewsOrganisation organisation)
    {
        var updated = false;
        if (NewsOrganisationId != organisation.Id)
        {
            NewsOrganisationId = organisation.Id;
            updated = true;
        }

        var name = organisation.Name.Trim();
        if (NewsOrganisationName != name)
        {
            NewsOrganisationName = name;
            updated = true;
        }

        if (NewsOrganisationRemoved != organisation.Removed)
        {
            NewsOrganisationRemoved = organisation.Removed;
            updated = true;
        }

        var searchTerms = organisation.SearchTerms?.Trim();
        if (NewsOrganisationSearchTerms != searchTerms)
        {
            NewsOrganisationSearchTerms = searchTerms;
            updated = true;
        }

        var language = organisation.Language?.Trim();
        if (NewsOrganisationLanguage != language)
        {
            NewsOrganisationLanguage = language;
            updated = true;
        }

        var updatedMetadata = false;
        if (NewsOrganisationMetadataVersion != organisation.Timestamp)
        {
            NewsOrganisationMetadataVersion = organisation.Timestamp;
            updatedMetadata = true;
        }

        return (updated, updatedMetadata);
    }
}
