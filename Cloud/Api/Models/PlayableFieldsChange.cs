using RedditPodcastPoster.Models.Services;

namespace Api.Models;

/// <summary>Curator playable-field edit (news report, TV-show episode). Null = unchanged.</summary>
public abstract class PlayableFieldsChange
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Release { get; set; }
    public string? Duration { get; set; }
    public bool? Posted { get; set; }
    public bool? Tweeted { get; set; }
    public bool? BlueskyPosted { get; set; }
    public bool? UnBluesky { get; set; }
    public bool? Explicit { get; set; }
    public bool? Ignored { get; set; }
    public bool? Removed { get; set; }
    public string[]? Subjects { get; set; }
    public string? SearchTerms { get; set; }
    public string? HashTag { get; set; }
    public string? Language { get; set; }
    public string[]? Guests { get; set; }

    /// <summary>
    /// Per-key link patch (same as film / episode): blank url+image removes the key; otherwise the key is
    /// resolved from the URL when present.
    /// </summary>
    public Dictionary<string, ServiceLink>? Services { get; set; }

    /// <summary>
    /// Additional YouTube uploads to keep, in display order (RPP #1526). Existing entries are identified by
    /// <c>youTubeId</c>; a new entry is added from a YouTube url. Entries left out are removed; [] removes all.
    /// </summary>
    public YouTubeAlternateChange[]? AdditionalYouTube { get; set; }

    /// <summary>
    /// Additional YouTube ids the client last saw. When <see cref="AdditionalYouTube"/> is sent, a stored id that is
    /// not in this list is a 409 and nothing is saved.
    /// </summary>
    public string[]? ExpectedAdditionalYouTubeIds { get; set; }

    /// <summary>
    /// The YouTube id of an additional upload to swap into the primary YouTube link. The previous primary becomes
    /// an additional upload. The id must be in <see cref="AdditionalYouTube"/> when that field is sent, otherwise
    /// it must already be on the item.
    /// </summary>
    public string? PromoteYouTubeId { get; set; }
}
