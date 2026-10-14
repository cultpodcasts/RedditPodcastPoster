using System.ComponentModel.DataAnnotations;

namespace RedditPodcastPoster.PodcastServices.YouTube.CandidatePreference;

/// <summary>
/// Options for choosing between near-identical YouTube candidates for one episode
/// (configuration section <c>youTubeCandidatePreference</c>).
/// Applied after <see cref="Services.PlaylistItemFinder"/> / <see cref="Services.YouTubeSearchResultFinder"/>
/// have picked a match, and only when the candidate list holds other contenders for the same episode.
/// </summary>
public class YouTubeCandidatePreferenceOptions
{
    public const string SectionName = "youTubeCandidatePreference";

    /// <summary>Turns candidate preference on. When false the finders' choice is returned unchanged.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Title tokens that mark a lower-quality upload (default: the 📱 mobile-phone emoji).</summary>
    public string[] PenalisedTitleTokens { get; set; } = ["\U0001F4F1"];

    /// <summary>Score removed from a candidate whose title contains any <see cref="PenalisedTitleTokens"/>.</summary>
    [Range(0, 1000)]
    public int PenalisedTitlePenalty { get; set; } = 10;

    /// <summary>Score removed from a candidate whose auto thumbnails show no motion (static-image upload).</summary>
    [Range(0, 1000)]
    public int StaticImagePenalty { get; set; } = 10;

    /// <summary>
    /// A video is static when the largest mean absolute RGB difference between its three auto thumbnails
    /// (i.ytimg.com/vi/&lt;id&gt;/1.jpg, 2.jpg, 3.jpg) is below this value (0-255 scale).
    /// Calibrated on 69 real videos: still-image uploads 0.07-3.10 (JPEG noise), lowest real video 6.93.
    /// </summary>
    [Range(0.0, 255.0)]
    public double StaticImageThreshold { get; set; } = 5.0;

    /// <summary>
    /// Frames whose pixel standard deviation is below this (0-255 scale) are treated as a blank or
    /// placeholder image, so motion is Unknown and no penalty applies (fail-open).
    /// </summary>
    [Range(0.0, 255.0)]
    public double PlaceholderMaxPixelStdDev { get; set; } = 1.0;

    /// <summary>Timeout for fetching one candidate's three thumbnails.</summary>
    [Range(1, 60)]
    public int ThumbnailFetchTimeoutSeconds { get; set; } = 5;

    /// <summary>Maximum other candidates considered besides the finder's choice (bounds thumbnail fetches).</summary>
    [Range(1, 10)]
    public int MaxContenders { get; set; } = 3;

    /// <summary>
    /// Minimum title similarity (FuzzySharp token-set ratio, 0-100, after removing penalised tokens) between the
    /// finder's choice and another candidate for it to count as a contender for the same episode.
    /// </summary>
    [Range(0, 100)]
    public int ContenderMinTitleSimilarity { get; set; } = 85;

    /// <summary>Maximum duration difference (seconds) between the finder's choice and a contender.</summary>
    [Range(0, 3600)]
    public int ContenderMaxDurationDifferenceSeconds { get; set; } = 120;
}