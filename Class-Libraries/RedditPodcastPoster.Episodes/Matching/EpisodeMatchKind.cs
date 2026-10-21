namespace RedditPodcastPoster.Episodes.Matching;

/// <summary>How <see cref="IEpisodePlatformMatcher.Classify"/> reached its decision.</summary>
public enum EpisodeMatchKind
{
    /// <summary>Not a match.</summary>
    None = 0,

    /// <summary>Same platform id, exact title, or the podcast's EpisodeMatchRegex. Never scored.</summary>
    Certain = 1,

    /// <summary>Fuzzy title + duration, or release + duration. Scored by the merge scorer when enabled.</summary>
    Heuristic = 2
}
