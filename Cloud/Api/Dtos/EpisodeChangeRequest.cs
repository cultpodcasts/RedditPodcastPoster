using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;

namespace Api.Dtos;

public class EpisodeChangeRequest
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("posted")]
    public bool? Posted { get; set; }

    [JsonPropertyName("tweeted")]
    public bool? Tweeted { get; set; }

    /// <summary>
    /// Same as Tweeted: true sets the legacy Bluesky posted flag (curator/manual mark; no network AT URI).
    /// true → false deletes the Bluesky post and clears state only when the post is gone.
    /// </summary>
    [JsonPropertyName("blueskyPosted")]
    public bool? BlueskyPosted { get; set; }

    /// <summary>
    /// Backward-compat alias for <c>blueskyPosted: false</c>: when true, delete the Bluesky post and clear
    /// state only when the post is gone.
    /// </summary>
    [JsonPropertyName("unBluesky")]
    public bool? UnBluesky { get; set; }

    [JsonPropertyName("ignored")]
    public bool? Ignored { get; set; }

    [JsonPropertyName("removed")]
    public bool? Removed { get; set; }

    /// <summary>
    /// #1484: <c>"Approve"</c> or <c>"Reject"</c> an episode awaiting approval (a split-off). Approving clears its
    /// stored <c>approvalStatus</c> (null means approved): it becomes public and the posting jobs may post it under
    /// their usual rules. Rejecting sets <c>"Rejected"</c>: it stays hidden and is never posted. 400 when rejecting an
    /// episode that is not held, or when it contradicts <c>removed</c>.
    /// </summary>
    [JsonPropertyName("approvalDecision")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Api.Models.ApprovalDecision? ApprovalDecision { get; set; }

    [JsonPropertyName("explicit")]
    public bool? Explicit { get; set; }

    [JsonPropertyName("release")]
    public DateTime? Release { get; set; }

    [JsonPropertyName("duration")]
    public string? Duration { get; set; }

    [JsonPropertyName("urls")]
    public ServiceUrls? Urls { get; set; }

    [JsonPropertyName("images")]
    public ServiceImageUrls? Images { get; set; }

    [JsonPropertyName("services")]
    public Dictionary<string, ServiceLink>? Services { get; set; }

    [JsonPropertyName("subjects")]
    public string[]? Subjects { get; set; }

    [JsonPropertyName("searchTerms")]
    public string? SearchTerms { get; set; }

    [JsonPropertyName("hashTag")]
    public string? HashTag { get; set; }

    [JsonPropertyName("lang")]
    public string? Language { get; set; }

    [JsonPropertyName("guests")]
    public string[]? Guests { get; set; }
}
