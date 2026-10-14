namespace RedditPodcastPoster.Episodes.Matching;

/// <summary>
/// Tunes the merge tie-break used only when more than one stored episode passes the platform matcher
/// for one incoming episode. Bound from <see cref="SectionName"/>; defaults apply when the section is absent.
/// Weights sum to the maximum score (100 by default).
/// </summary>
public sealed class EpisodeMergeTieBreakOptions
{
    public const string SectionName = "episodeMergeTieBreak";

    /// <summary>When false, multiple matches fail as before without scoring.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When true, a single match reached by a heuristic path (fuzzy title + duration, or release + duration) is
    /// scored too and rejected below <see cref="SingleHeuristicMinScore"/>; the incoming episode is then added as new.
    /// Certain matches (platform id, exact title, EpisodeMatchRegex) are never scored.
    /// </summary>
    public bool ScoreSingleHeuristicMatches { get; set; } = true;

    /// <summary>Minimum score for a single heuristic match to be accepted.</summary>
    public int SingleHeuristicMinScore { get; set; } = 60;

    /// <summary>Points for identical duration, falling linearly to zero at <see cref="DurationWindow"/>.</summary>
    public int DurationWeight { get; set; } = 30;

    public TimeSpan DurationWindow { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Points for release proximity (after the podcast's YouTube publication offset for YouTube/audio pairs),
    /// falling linearly to zero at <see cref="ReleaseWindow"/>.
    /// </summary>
    public int ReleaseWeight { get; set; } = 30;

    public TimeSpan ReleaseWindow { get; set; } = TimeSpan.FromDays(30);

    /// <summary>Points scaled by the weighted fuzzy title ratio (0-100).</summary>
    public int TitleWeight { get; set; } = 20;

    /// <summary>Points scaled by the weighted fuzzy ratio of the description intros.</summary>
    public int DescriptionWeight { get; set; } = 10;

    /// <summary>Characters of each description compared.</summary>
    public int DescriptionCompareMaxChars { get; set; } = 500;

    /// <summary>Points for two or more shared subjects; half for one.</summary>
    public int SubjectWeight { get; set; } = 10;

    /// <summary>The winner must reach this score.</summary>
    public int MinScore { get; set; } = 60;

    /// <summary>The winner must lead the runner-up by at least this many points.</summary>
    public int MinMargin { get; set; } = 15;

    public const string ValidationMessage =
        "episodeMergeTieBreak: weights and MinMargin must be >= 0, windows and DescriptionCompareMaxChars > 0, " +
        "and MinScore between 0 and the sum of the weights.";

    public int MaxScore => DurationWeight + ReleaseWeight + TitleWeight + DescriptionWeight + SubjectWeight;

    public static bool IsValid(EpisodeMergeTieBreakOptions o) =>
        o.DurationWeight >= 0 && o.ReleaseWeight >= 0 && o.TitleWeight >= 0 &&
        o.DescriptionWeight >= 0 && o.SubjectWeight >= 0 && o.MinMargin >= 0 &&
        o.DurationWindow > TimeSpan.Zero && o.ReleaseWindow > TimeSpan.Zero &&
        o.DescriptionCompareMaxChars > 0 &&
        o.MinScore >= 0 && o.MinScore <= o.MaxScore;
}
