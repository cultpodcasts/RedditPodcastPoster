using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Models.News;

public class NewsReport
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public Guid Id { get; set; }

    [JsonPropertyName("newsOrganisationId")]
    [JsonPropertyOrder(2)]
    public Guid NewsOrganisationId { get; set; }

    [JsonPropertyName("title")]
    [JsonPropertyOrder(10)]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonPropertyOrder(20)]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("release")]
    [JsonPropertyOrder(30)]
    public DateTime Release { get; set; }

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

    [JsonPropertyName("ids")]
    [JsonPropertyOrder(53)]
    public EpisodeIds? Ids { get; set; }

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
    public Dictionary<string, EpisodeServiceLink>? Services { get; set; }

    [JsonPropertyName("_ts")]
    public long Timestamp { get; set; }

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
