namespace RedditPodcastPoster.Episodes.Matching.Candidates;

/// <summary>
/// Tunables for <see cref="CatalogueCandidatePolicy"/>, bound from the optional
/// <c>catalogueCandidatePolicy</c> configuration section.
/// </summary>
public sealed class CatalogueCandidatePolicyOptions
{
    public const string SectionName = "catalogueCandidatePolicy";

    /// <summary>
    /// Show-feed page size. Defaults to 50 because that is the Spotify show-episodes API maximum, so the recent
    /// page is always a single call (AGENTS.md pagination rule: never page the catalogue for one lookup).
    /// </summary>
    public int RecentShowEpisodesPageSize { get; set; } = 50;

    /// <summary>
    /// How recent a release must be to read the show feed first. Defaults to
    /// <see cref="EpisodeReleaseTolerance.YouTubeAuthorityToAudioReleaseConsiderationThreshold"/> (14 days) on
    /// purpose: that is how far apart YouTube and audio releases may be and still match, so any audio counterpart
    /// worth matching is still near the top of the feed.
    /// </summary>
    public TimeSpan RecentWindow { get; set; } =
        EpisodeReleaseTolerance.YouTubeAuthorityToAudioReleaseConsiderationThreshold;
}
