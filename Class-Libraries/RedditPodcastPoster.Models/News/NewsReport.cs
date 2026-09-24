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

    [JsonPropertyName("newsOrganisationName")]
    [JsonPropertyOrder(90)]
    public string? NewsOrganisationName { get; set; }

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

        if (ParentRemoved != organisation.Removed)
        {
            ParentRemoved = organisation.Removed;
            updated = true;
        }

        var searchTerms = organisation.SearchTerms?.Trim();
        if (PublisherSearchTerms != searchTerms)
        {
            PublisherSearchTerms = searchTerms;
            updated = true;
        }

        var language = organisation.Language?.Trim();
        if (PublisherLanguage != language)
        {
            PublisherLanguage = language;
            updated = true;
        }

        var updatedMetadata = false;
        if (ParentMetadataVersion != organisation.Timestamp)
        {
            ParentMetadataVersion = organisation.Timestamp;
            updatedMetadata = true;
        }

        return (updated, updatedMetadata);
    }
}
