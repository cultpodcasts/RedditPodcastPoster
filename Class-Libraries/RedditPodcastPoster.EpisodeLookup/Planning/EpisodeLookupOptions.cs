using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Candidates;

namespace RedditPodcastPoster.EpisodeLookup;

/// <summary>
/// Every tunable of the episode lookup planner, bound from the optional <c>episodeLookup</c> configuration section and
/// validated at startup (<c>ValidateOnStart</c>). Change a threshold here or in configuration, never in code.
/// See <c>RedditPodcastPoster.EpisodeLookup/README.md</c>.
/// </summary>
public sealed class EpisodeLookupOptions
{
    public const string SectionName = "episodeLookup";

    /// <summary>Shows with at most this many episodes are read in full in any flow (default 200).</summary>
    public int FullShowReadMaxEpisodes { get; set; } = 200;

    /// <summary>
    /// A non-index release within this window reads the newest page before searching. Defaults to
    /// <see cref="EpisodeReleaseTolerance.YouTubeAuthorityToAudioReleaseConsiderationThreshold"/> (14 days).
    /// </summary>
    public TimeSpan RecentWindow { get; set; } =
        EpisodeReleaseTolerance.YouTubeAuthorityToAudioReleaseConsiderationThreshold;

    /// <summary>
    /// Safety cap on pages for a full read of a small show (default 20). Apple pages can be smaller than Spotify's 50,
    /// so this is a page budget, separate from <see cref="FullShowReadMaxEpisodes"/>.
    /// </summary>
    public int FullShowReadMaxPages { get; set; } = 20;

    /// <summary>
    /// Time budget for a small-show full read outside the Index flow (default 20 s), covering page reads and subject
    /// classification, checked between pages (soft cap: may overshoot by one page). When it is spent the lookup falls
    /// back to a title search. Index full reads are not budgeted.
    /// Real case: submission 4a7d8838 spent ~45 s per platform per item on The Compendium (185/159 episodes).
    /// </summary>
    public TimeSpan FullShowReadBudget { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>Safety cap for the Index flow's newest-pages read, otherwise bounded only by date (default 20).</summary>
    public int IndexMaxPages { get; set; } = 20;

    /// <summary>Bounded-walk page cap for Spotify (pages of 50; default 4).</summary>
    public int SpotifyBoundedWalkPages { get; set; } = 4;

    /// <summary>Bounded-walk page cap for Apple amp-api pages (default 5).</summary>
    public int AppleBoundedWalkPages { get; set; } = 5;

    /// <summary>Per-request timeout guard for Spotify and Apple catalogue calls, the same in every flow (default 10 s).</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public const string ValidationMessage =
        "episodeLookup: FullShowReadMaxEpisodes must be 0..1000, FullShowReadMaxPages 1..100, RecentWindow > 0, IndexMaxPages 1..100, " +
        "SpotifyBoundedWalkPages and AppleBoundedWalkPages 1..20, RequestTimeout 1..120 seconds, FullShowReadBudget 1..300 seconds.";

    public static bool IsValid(EpisodeLookupOptions options) =>
        options.FullShowReadMaxEpisodes is >= 0 and <= 1000 &&
        options.FullShowReadMaxPages is >= 1 and <= 100 &&
        options.RecentWindow > TimeSpan.Zero &&
        options.IndexMaxPages is >= 1 and <= 100 &&
        options.SpotifyBoundedWalkPages is >= 1 and <= 20 &&
        options.AppleBoundedWalkPages is >= 1 and <= 20 &&
        options.RequestTimeout >= TimeSpan.FromSeconds(1) && options.RequestTimeout <= TimeSpan.FromSeconds(120) &&
        options.FullShowReadBudget >= TimeSpan.FromSeconds(1) && options.FullShowReadBudget <= TimeSpan.FromSeconds(300);

    /// <summary>Bounded-walk cap for <paramref name="platform"/> (<see cref="CataloguePlatforms"/>); 1 for an unknown platform.</summary>
    public int BoundedWalkPagesFor(string platform) => platform switch
    {
        CataloguePlatforms.Spotify => SpotifyBoundedWalkPages,
        CataloguePlatforms.Apple => AppleBoundedWalkPages,
        _ => 1
    };
}
