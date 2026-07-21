using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Models;

public record PodcastServiceSearchCriteria(
    string ShowName,
    string ShowDescription,
    string Publisher,
    string EpisodeTitle,
    string EpisodeDescription,
    DateTime Release,
    TimeSpan Duration)
{
    public string? SpotifyTitle { get; set; }
    public string? AppleTitle { get; set; }

    /// <summary>
    /// Platform the criteria originated from (the submitted URL's service). When YouTube, audio
    /// platform lookups must shift <see cref="Release"/> back by the podcast's YouTube publishing delay.
    /// </summary>
    public Service? SourceAuthority { get; init; }
}
