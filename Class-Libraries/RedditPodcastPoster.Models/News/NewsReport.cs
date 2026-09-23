using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Services;

namespace RedditPodcastPoster.Models.News;

/// <summary>
/// Playable news report under a <see cref="NewsOrganisation"/>.
/// Platform presence is <see cref="Services"/> only — no provider-id fields.
/// Release is a calendar date (no time-of-day).
/// </summary>
[CosmosSelector(ModelType.NewsReport)]
public sealed class NewsReport : CosmosSelector
{
    public NewsReport()
    {
        Id = Guid.NewGuid();
        ModelType = ModelType.NewsReport;
    }

    public NewsReport(string title) : this()
    {
        Title = title;
        FileKey = FileKeyFactory.GetFileKey(title);
    }

    [JsonPropertyName("newsOrganisationId")]
    [JsonPropertyOrder(3)]
    public Guid NewsOrganisationId { get; set; }

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

    [JsonPropertyName("newsOrganisationName")]
    [JsonPropertyOrder(90)]
    public string? NewsOrganisationName { get; set; }

    [JsonPropertyName("services")]
    [JsonPropertyOrder(151)]
    public Dictionary<string, ServiceLink>? Services { get; set; }

    public bool SetNewsOrganisationProperties(NewsOrganisation organisation)
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

        return updated;
    }
}
