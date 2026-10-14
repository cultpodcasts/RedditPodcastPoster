using Microsoft.Extensions.Options;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Text.Matchers;

namespace RedditPodcastPoster.Episodes.Matching;

/// <summary>
/// Graded composite score (duration closeness, release proximity after the YouTube publication offset,
/// fuzzy title, fuzzy description intro, shared subjects) used to pick a clear winner among several
/// matched candidates. Shares the signal model of <see cref="CrossPlatformMatchScorer"/> but grades each
/// signal instead of awarding pass/fail points, because every candidate here has already passed the matcher.
/// </summary>
public sealed class EpisodeMergeCandidateScorer(IOptions<EpisodeMergeTieBreakOptions> options)
    : IEpisodeMergeCandidateScorer
{
    public const string DisabledReason = "tie-break disabled";
    public const string DuplicatePlatformIdReason = "candidates share a platform id (data problem)";
    public const string PlatformIdMatchReason = "a candidate already carries the incoming platform id";
    public const string BelowMinScoreReason = "winner below MinScore";
    public const string BelowMinMarginReason = "winner lead below MinMargin";
    public const string TieBrokenReason = "tie broken";
    public const string SingleScoringDisabledReason = "single heuristic scoring disabled";
    public const string SingleAcceptedReason = "single heuristic match accepted";
    public const string SingleRejectedReason = "single heuristic match below SingleHeuristicMinScore";

    private readonly EpisodeMergeTieBreakOptions _options = options.Value;

    public EpisodeMergeTieBreakDecision Decide(Podcast podcast, Episode incoming, IReadOnlyList<Episode> candidates)
    {
        if (candidates.Count < 2)
        {
            throw new ArgumentException("The merge tie-break needs at least two candidates.", nameof(candidates));
        }

        if (!_options.Enabled)
        {
            return new EpisodeMergeTieBreakDecision(null, [], DisabledReason);
        }

        if (CandidatesSharePlatformId(candidates))
        {
            return new EpisodeMergeTieBreakDecision(null, [], DuplicatePlatformIdReason);
        }

        if (candidates.Any(c => SharesPlatformId(c, incoming)))
        {
            return new EpisodeMergeTieBreakDecision(null, [], PlatformIdMatchReason);
        }

        var scores = candidates
            .Select(c => new EpisodeMergeCandidateScore(c, Score(podcast, c, incoming)))
            .OrderByDescending(s => s.Score)
            .ToList();

        var best = scores[0];
        if (best.Score < _options.MinScore)
        {
            return new EpisodeMergeTieBreakDecision(null, scores, BelowMinScoreReason);
        }

        if (best.Score - scores[1].Score < _options.MinMargin)
        {
            return new EpisodeMergeTieBreakDecision(null, scores, BelowMinMarginReason);
        }

        return new EpisodeMergeTieBreakDecision(best.Candidate, scores, TieBrokenReason);
    }

    public EpisodeMergeSingleMatchDecision DecideSingleHeuristic(Podcast podcast, Episode incoming, Episode candidate)
    {
        if (!_options.Enabled || !_options.ScoreSingleHeuristicMatches)
        {
            return new EpisodeMergeSingleMatchDecision(true, null, SingleScoringDisabledReason);
        }

        var score = Score(podcast, candidate, incoming);
        return score >= _options.SingleHeuristicMinScore
            ? new EpisodeMergeSingleMatchDecision(true, score, SingleAcceptedReason)
            : new EpisodeMergeSingleMatchDecision(false, score, SingleRejectedReason);
    }

    internal int Score(Podcast podcast, Episode existing, Episode incoming)
    {
        var score = Graded(_options.DurationWeight, (existing.Length - incoming.Length).Duration(), _options.DurationWindow);
        score += Graded(_options.ReleaseWeight, ReleaseDistance(podcast, existing, incoming), _options.ReleaseWindow);
        score += _options.TitleWeight * FuzzyMatcher.Score(existing.Title, incoming.Title) / 100.0;
        score += _options.DescriptionWeight *
                 FuzzyMatcher.Score(Intro(existing.Description), Intro(incoming.Description)) / 100.0;
        score += ScoreSubjects(podcast, existing, incoming);
        return (int)Math.Round(score, MidpointRounding.AwayFromZero);
    }

    private static double Graded(int weight, TimeSpan distance, TimeSpan window) =>
        distance >= window ? 0 : weight * (1 - distance.TotalSeconds / window.TotalSeconds);

    /// <summary>
    /// |YouTube release - (audio release + YouTube publication offset)| for a YouTube/audio pair;
    /// plain release distance otherwise.
    /// </summary>
    private static TimeSpan ReleaseDistance(Podcast podcast, Episode existing, Episode incoming)
    {
        var existingYouTube = existing.HasYouTubeIdentity();
        var incomingYouTube = incoming.HasYouTubeIdentity();
        if (existingYouTube != incomingYouTube)
        {
            var (youTube, audio) = existingYouTube ? (existing, incoming) : (incoming, existing);
            return (youTube.ReleaseUtc - audio.ReleaseUtc.Add(podcast.YouTubePublishingDelay())).Duration();
        }

        return (existing.ReleaseUtc - incoming.ReleaseUtc).Duration();
    }

    private string? Intro(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var trimmed = description.Trim();
        return trimmed.Length <= _options.DescriptionCompareMaxChars
            ? trimmed
            : trimmed[.._options.DescriptionCompareMaxChars];
    }

    private double ScoreSubjects(Podcast podcast, Episode existing, Episode incoming)
    {
        var shared = CatalogueMatchScorer.CountSharedSubjects(
            existing.Subjects,
            incoming.Subjects,
            new CatalogueSubjectScoreFilters(podcast.DefaultSubject, podcast.IgnoredSubjects));
        return shared switch
        {
            >= 2 => _options.SubjectWeight,
            1 => _options.SubjectWeight / 2.0,
            _ => 0
        };
    }

    private static bool CandidatesSharePlatformId(IReadOnlyList<Episode> candidates) =>
        HasDuplicate(candidates, EpisodeServicePresence.YouTubeEpisodeId) ||
        HasDuplicate(candidates, EpisodeServicePresence.SpotifyEpisodeId) ||
        HasDuplicate(candidates, e => EpisodeServicePresence.AppleEpisodeId(e)?.ToString());

    private static bool HasDuplicate(IReadOnlyList<Episode> candidates, Func<Episode, string?> id) =>
        candidates
            .Select(id)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x, StringComparer.Ordinal)
            .Any(g => g.Count() > 1);

    private static bool SharesPlatformId(Episode candidate, Episode incoming) =>
        Same(EpisodeServicePresence.YouTubeEpisodeId(candidate), EpisodeServicePresence.YouTubeEpisodeId(incoming)) ||
        Same(EpisodeServicePresence.SpotifyEpisodeId(candidate), EpisodeServicePresence.SpotifyEpisodeId(incoming)) ||
        Same(EpisodeServicePresence.AppleEpisodeId(candidate)?.ToString(),
            EpisodeServicePresence.AppleEpisodeId(incoming)?.ToString());

    private static bool Same(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && string.Equals(left, right, StringComparison.Ordinal);
}
