using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Matching;

/// <summary>
/// Breaks a tie when several stored episodes match one incoming episode. Never consulted for a single match.
/// </summary>
public interface IEpisodeMergeCandidateScorer
{
    /// <summary>Picks a clear winner among <paramref name="candidates"/>, or none.</summary>
    /// <param name="candidates">Two or more stored episodes that each passed the platform matcher.</param>
    /// <returns>A decision whose <c>Scores</c> are highest first (empty when the candidates were not scored).</returns>
    /// <exception cref="ArgumentException">Fewer than two candidates.</exception>
    EpisodeMergeTieBreakDecision Decide(Podcast podcast, Episode incoming, IReadOnlyList<Episode> candidates);

    /// <summary>
    /// Decides whether a single match reached by a heuristic path is strong enough to merge. Not scored (accepted)
    /// when the tie-break or <c>ScoreSingleHeuristicMatches</c> is disabled.
    /// </summary>
    EpisodeMergeSingleMatchDecision DecideSingleHeuristic(Podcast podcast, Episode incoming, Episode candidate);
}

/// <param name="Accepted">False when the score is below <c>SingleHeuristicMinScore</c>; the incoming episode is then new.</param>
/// <param name="Score">The candidate's score, or null when not scored.</param>
/// <param name="MinScore">The threshold applied, or null when not scored.</param>
public sealed record EpisodeMergeSingleMatchDecision(bool Accepted, int? Score, string Reason, int? MinScore = null);

public sealed record EpisodeMergeCandidateScore(Episode Candidate, int Score);

/// <param name="Winner">The chosen stored episode, or null when the merge must fail.</param>
/// <param name="Scores">Every candidate's score, highest first (empty when not scored).</param>
/// <param name="Reason">Why the decision was made, for logs and the failure report.</param>
public sealed record EpisodeMergeTieBreakDecision(
    Episode? Winner,
    IReadOnlyList<EpisodeMergeCandidateScore> Scores,
    string Reason);
