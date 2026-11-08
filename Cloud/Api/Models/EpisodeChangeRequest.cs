using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;

namespace Api.Models;

public class EpisodeChangeRequest
{
    public string? Title { get; set; }

    public string? Description { get; set; }

    public bool? Posted { get; set; }

    public bool? Tweeted { get; set; }

    /// <summary>
    /// Same as Tweeted: true sets the legacy Bluesky posted flag (curator/manual mark; no network AT URI).
    /// true → false deletes the Bluesky post and clears state only when the post is gone.
    /// </summary>
    public bool? BlueskyPosted { get; set; }

    /// <summary>
    /// Backward-compat alias for <c>blueskyPosted: false</c>: when true, delete the Bluesky post and clear
    /// state only when the post is gone.
    /// </summary>
    public bool? UnBluesky { get; set; }

    public bool? Ignored { get; set; }

    public bool? Removed { get; set; }

    public bool? Explicit { get; set; }

    public DateTime? Release { get; set; }

    public string? Duration { get; set; }

    public ServiceUrls? Urls { get; set; }

    public ServiceImageUrls? Images { get; set; }

    public Dictionary<string, ServiceLink>? Services { get; set; }

    public string[]? Subjects { get; set; }

    public string? SearchTerms { get; set; }

    public string? HashTag { get; set; }

    public string? Language { get; set; }

    public string[]? Guests { get; set; }

    public bool HasChange =>
        Title != null ||
        Description != null ||
        Posted != null ||
        Tweeted != null ||
        BlueskyPosted != null ||
        UnBluesky != null ||
        Ignored != null ||
        Removed != null ||
        Explicit != null ||
        Release != null ||
        Duration != null ||
        Urls != null ||
        Images != null ||
        Services != null ||
        Subjects != null ||
        SearchTerms != null ||
        HashTag != null ||
        Language != null ||
        Guests != null;

    /// <summary>
    /// Homepage JSON does not include guests, search terms, hash tags, or social un-post flags.
    /// A guests-only curator POST must not wait on a full homepage republish.
    /// </summary>
    public bool HasHomepageAffectingChange =>
        Title != null ||
        Description != null ||
        Posted != null ||
        Ignored != null ||
        Removed != null ||
        Explicit != null ||
        Release != null ||
        Duration != null ||
        Urls != null ||
        Images != null ||
        Services != null ||
        Subjects != null ||
        Language != null;
}
