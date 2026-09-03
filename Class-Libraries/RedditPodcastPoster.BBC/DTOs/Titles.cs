using System.Text.Json.Serialization;
using RedditPodcastPoster.BBC.Extractors;

namespace RedditPodcastPoster.BBC.DTOs;

public class Titles
{
    [JsonPropertyName("primary")]
    public String? Primary { get; set; }

    [JsonPropertyName("secondary")]
    public String? Secondary { get; set; }

    [JsonPropertyName("tertiary")]
    public String? Tertiary { get; set; }

    [JsonPropertyName("entity_title")]
    public String? EntityTitle { get; set; }

    public string Title => EntityTitle ?? Tertiary ?? Secondary ?? Primary ?? string.Empty;

    /// <summary>
    /// Sounds <c>titles.primary</c> is the programme/brand when it differs from the episode title.
    /// </summary>
    public string? SeriesName => BbcSeriesName.FromProgrammeBrand(Primary, Title);
}