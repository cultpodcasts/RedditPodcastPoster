using System.Text.Json.Serialization;

namespace Api.Dtos;

public class SubmitUrlLookupResponse
{
    [JsonPropertyName("known")]
    public required bool Known { get; init; }

    [JsonPropertyName("kind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Kind { get; init; }

    [JsonPropertyName("podcastId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? PodcastId { get; init; }

    [JsonPropertyName("podcastName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PodcastName { get; init; }

    [JsonPropertyName("ambiguous")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Ambiguous { get; init; }

    [JsonPropertyName("podcastIds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<Guid>? PodcastIds { get; init; }

    public static SubmitUrlLookupResponse From(RedditPodcastPoster.UrlSubmission.Models.UrlMembershipLookupResult result) =>
        new()
        {
            Known = result.Known,
            Kind = result.Kind,
            PodcastId = result.PodcastId,
            PodcastName = result.PodcastName,
            Ambiguous = result.Ambiguous,
            PodcastIds = result.PodcastIds
        };
}
